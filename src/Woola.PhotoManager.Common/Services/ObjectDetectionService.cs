using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Woola.PhotoManager.Common.Models;

namespace Woola.PhotoManager.Common.Services;

public class ObjectDetectionService : IObjectDetectionService, IDisposable
{
    private InferenceSession? _session;
    private readonly string _modelPath;
    private bool _isInitialized;

    private const float ConfidenceThreshold = 0.65f;  // Subir de 0.35 a 0.65
    private const float NmsThreshold = 0.45f;         // Eliminar duplicados
    private const int ImageSize = 640;

    private readonly string[] _classNames = new string[]
    {
    "person", "bicycle", "car", "motorbike", "aeroplane", "bus", "train", "truck",
    "boat", "traffic light", "fire hydrant", "stop sign", "parking meter", "bench", "bird",
    "cat", "dog", "horse", "sheep", "cow", "elephant", "bear", "zebra", "giraffe",
    "backpack", "umbrella", "handbag", "tie", "suitcase", "frisbee", "skis", "snowboard",
    "sports ball", "kite", "baseball bat", "baseball glove", "skateboard", "surfboard", "tennis racket",
    "bottle", "wine glass", "cup", "fork", "knife", "spoon", "bowl", "banana", "apple",
    "sandwich", "orange", "broccoli", "carrot", "hot dog", "pizza", "donut", "cake",
    "chair", "sofa", "pottedplant", "bed", "diningtable", "toilet", "tvmonitor", "laptop",
    "mouse", "remote", "keyboard", "cell phone", "microwave", "oven", "toaster", "sink",
    "refrigerator", "book", "clock", "vase", "scissors", "teddy bear", "hair drier", "toothbrush"
    };


    public ObjectDetectionService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var modelsDir = Path.Combine(appData, "Woola", "Models");
        Directory.CreateDirectory(modelsDir);
        _modelPath = Path.Combine(modelsDir, "yolov8n.onnx");
    }

    private List<DetectedObject> ApplyNms(List<DetectedObject> detections, float iouThreshold)
    {
        if (detections.Count <= 1) return detections;

        // Ordenar por confianza descendente
        var sorted = detections.OrderByDescending(d => d.Confidence).ToList();
        var results = new List<DetectedObject>();

        while (sorted.Any())
        {
            var best = sorted.First();
            results.Add(best);
            sorted.RemoveAt(0);

            for (int i = sorted.Count - 1; i >= 0; i--)
            {
                var iou = CalculateIoU(best, sorted[i]);
                if (iou > iouThreshold)
                {
                    sorted.RemoveAt(i);
                }
            }
        }

        return results;
    }

    private float CalculateIoU(DetectedObject a, DetectedObject b)
    {
        // Calcular intersección
        var x1 = Math.Max(a.X - a.Width / 2, b.X - b.Width / 2);
        var y1 = Math.Max(a.Y - a.Height / 2, b.Y - b.Height / 2);
        var x2 = Math.Min(a.X + a.Width / 2, b.X + b.Width / 2);
        var y2 = Math.Min(a.Y + a.Height / 2, b.Y + b.Height / 2);

        if (x2 < x1 || y2 < y1) return 0f;

        var intersection = (x2 - x1) * (y2 - y1);
        var areaA = a.Width * a.Height;
        var areaB = b.Width * b.Height;
        var union = areaA + areaB - intersection;

        return intersection / union;
    }

    public async Task<bool> IsModelAvailable()
    {
        return await Task.Run(() => File.Exists(_modelPath));
    }

    public async Task DownloadModelIfNeededAsync()
    {
        if (File.Exists(_modelPath))
            return;

        var modelUrl = "https://huggingface.co/unity/sentis-YOLOv8n/resolve/main/yolov8n.onnx";

        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromMinutes(10);

        var response = await client.GetAsync(modelUrl);
        response.EnsureSuccessStatusCode();

        await using var fs = new FileStream(_modelPath, FileMode.Create);
        await response.Content.CopyToAsync(fs);
    }

    private void InitializeModel()
    {
        if (_isInitialized) return;

        if (!File.Exists(_modelPath))
            throw new FileNotFoundException($"Modelo no encontrado: {_modelPath}");

        _session = new InferenceSession(_modelPath);
        InspectModel();  // ← Agregar esta línea
        _isInitialized = true;
    }

    public async Task<List<DetectedObject>> DetectObjectsAsync(string imagePath)
    {
        return await Task.Run(() =>
        {
            try
            {
                InitializeModel();

                using var image = Image.Load<Rgb24>(imagePath);
                using var resizedImage = image.Clone(ctx => ctx.Resize(ImageSize, ImageSize));

                var inputTensor = new DenseTensor<float>(new[] { 1, 3, ImageSize, ImageSize });

                resizedImage.ProcessPixelRows(accessor =>
                {
                    for (int y = 0; y < ImageSize; y++)
                    {
                        var pixelRow = accessor.GetRowSpan(y);
                        for (int x = 0; x < ImageSize; x++)
                        {
                            inputTensor[0, 0, y, x] = pixelRow[x].R / 255.0f;
                            inputTensor[0, 1, y, x] = pixelRow[x].G / 255.0f;
                            inputTensor[0, 2, y, x] = pixelRow[x].B / 255.0f;
                        }
                    }
                });

                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor("images", inputTensor)
                };

                using var results = _session!.Run(inputs);
                var output = results.First().AsTensor<float>();

                var detections = ParseDetections(output);
                return detections;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en detección de objetos: {ex.Message}");
                return new List<DetectedObject>();
            }
        });
    }


    private List<DetectedObject> ParseDetections(Tensor<float> output)
    {
        var detections = new List<DetectedObject>();
        var dimensions = output.Dimensions;

        if (dimensions.Length < 3) return detections;

        // YOLOv8: [1, 84, 8400]
        int numClasses = dimensions[1] - 4;  // 84 - 4 = 80
        int numDetections = dimensions[2];   // 8400

        Console.WriteLine($"YOLOv8: {numDetections} detecciones, {numClasses} clases");

        for (int i = 0; i < numDetections; i++)
        {
            // Obtener las 4 coordenadas del bounding box
            float x = output[0, 0, i];
            float y = output[0, 1, i];
            float w = output[0, 2, i];
            float h = output[0, 3, i];

            // Encontrar la clase con mayor confianza (índices 4 a 83)
            float maxConfidence = 0;
            int maxClassIndex = -1;

            for (int j = 0; j < numClasses && j < _classNames.Length; j++)
            {
                float confidence = output[0, 4 + j, i];

                // Aplicar sigmoide para normalizar
                float normalizedConf = 1.0f / (1.0f + (float)Math.Exp(-confidence));

                if (normalizedConf > maxConfidence)
                {
                    maxConfidence = normalizedConf;
                    maxClassIndex = j;
                }
            }

            // Umbral de confianza
            if (maxConfidence > 0.5f && maxClassIndex >= 0)
            {
                detections.Add(new DetectedObject
                {
                    ClassName = _classNames[maxClassIndex],
                    Confidence = maxConfidence,
                    X = Math.Clamp(x, 0, 1),
                    Y = Math.Clamp(y, 0, 1),
                    Width = Math.Clamp(w, 0, 1),
                    Height = Math.Clamp(h, 0, 1)
                });
            }
        }

        // Aplicar NMS para eliminar duplicados
        var uniqueDetections = ApplyNms(detections, 0.45f);

        Console.WriteLine($"Detecciones: {uniqueDetections.Count}");
        return uniqueDetections;
    }





    private void InspectModel()
    {
        if (_session == null) return;

        Console.WriteLine("=== INSPECCIONANDO MODELO ===");

        // Mostrar inputs
        Console.WriteLine("INPUTS:");
        foreach (var input in _session.InputMetadata)
        {
            Console.WriteLine($"  Nombre: {input.Key}");
            Console.WriteLine($"  Dimensiones: [{string.Join(", ", input.Value.Dimensions)}]");
            Console.WriteLine($"  Tipo: {input.Value.ElementType}");
        }

        // Mostrar outputs
        Console.WriteLine("OUTPUTS:");
        foreach (var output in _session.OutputMetadata)
        {
            Console.WriteLine($"  Nombre: {output.Key}");
            Console.WriteLine($"  Dimensiones: [{string.Join(", ", output.Value.Dimensions)}]");
            Console.WriteLine($"  Tipo: {output.Value.ElementType}");
        }

        Console.WriteLine("===========================");
    }

    public void Dispose()
    {
        _session?.Dispose();
    }
}