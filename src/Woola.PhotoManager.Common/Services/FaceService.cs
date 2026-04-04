using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using OpenCvSharp.Dnn;
using Woola.PhotoManager.Common.Models;

namespace Woola.PhotoManager.Common.Services;

public class FaceService : IFaceService, IDisposable
{
    private Net? _faceDetector;
    private InferenceSession? _faceEncoder;
    private readonly string _detectorConfigPath;
    private readonly string _detectorModelPath;
    private readonly string _encoderPath;
    private bool _isInitialized;

    private const float ConfidenceThreshold = 0.3f;
    private const int EmbeddingSize = 128;

    public FaceService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var modelsDir = Path.Combine(appData, "Woola", "Models");
        Directory.CreateDirectory(modelsDir);

        _detectorConfigPath = Path.Combine(modelsDir, "deploy.prototxt");
        _detectorModelPath = Path.Combine(modelsDir, "res10_300x300_ssd_iter_140000.caffemodel");
        _encoderPath = Path.Combine(modelsDir, "facenet.onnx");
    }

    public async Task<bool> IsModelAvailable()
    {
        return await Task.Run(() => File.Exists(_detectorModelPath) && File.Exists(_encoderPath));
    }

    public async Task DownloadModelsIfNeededAsync()
    {
        // Detector de rostros OpenCV
        if (!File.Exists(_detectorModelPath))
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(10);

            var modelUrl = "https://github.com/opencv/opencv_3rdparty/raw/dnn_samples_face_detector_20170830/res10_300x300_ssd_iter_140000_fp16.caffemodel";
            var configUrl = "https://raw.githubusercontent.com/opencv/opencv/master/samples/dnn/face_detector/deploy.prototxt";

            var modelData = await client.GetByteArrayAsync(modelUrl);
            await File.WriteAllBytesAsync(_detectorModelPath, modelData);

            var configData = await client.GetByteArrayAsync(configUrl);
            await File.WriteAllBytesAsync(_detectorConfigPath, configData);
        }

        // FaceNet para embeddings
        if (!File.Exists(_encoderPath))
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(15);

            var url = "https://github.com/serengil/deepface_models/raw/main/facenet_model_weights.h5";

            try
            {
                var data = await client.GetByteArrayAsync(url);
                await File.WriteAllBytesAsync(_encoderPath, data);
            }
            catch
            {
                // Crear archivo vacío para no reintentar
                await File.WriteAllBytesAsync(_encoderPath, Array.Empty<byte>());
            }
        }
    }

    private void InitializeModels()
    {
        if (_isInitialized) return;

        Console.WriteLine("=== INICIALIZANDO FACE DETECTOR ===");
        Console.WriteLine($"Modelo path: {_detectorModelPath}");
        Console.WriteLine($"Config path: {_detectorConfigPath}");
        Console.WriteLine($"Modelo existe: {File.Exists(_detectorModelPath)}");
        Console.WriteLine($"Config existe: {File.Exists(_detectorConfigPath)}");

        if (File.Exists(_detectorModelPath) && File.Exists(_detectorConfigPath))
        {
            try
            {
                _faceDetector = Net.ReadNetFromCaffe(_detectorConfigPath, _detectorModelPath);
                Console.WriteLine("FaceDetector cargado correctamente");

                // Verificar que no sea nulo
                if (_faceDetector != null)
                {
                    Console.WriteLine("FaceDetector no es nulo");
                }
                else
                {
                    Console.WriteLine("ERROR: FaceDetector es nulo después de cargar");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR cargando modelo: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine("ERROR: Archivos del modelo no encontrados");
        }

        _isInitialized = true;
    }


    public async Task<List<DetectedFace>> DetectFacesAsync(string imagePath)
    {
        return await Task.Run(() =>
        {
            var faces = new List<DetectedFace>();

            try
            {
                InitializeModels();

                if (_faceDetector == null)
                {
                    Console.WriteLine("FaceDetector es null, no se puede detectar");
                    return faces;
                }

                using var image = Cv2.ImRead(imagePath);
                if (image.Empty())
                {
                    Console.WriteLine("No se pudo cargar la imagen");
                    return faces;
                }

                Console.WriteLine($"Imagen cargada: {image.Width}x{image.Height}");

                var blob = CvDnn.BlobFromImage(image, 1.0, new Size(300, 300), new Scalar(104, 177, 123));
                _faceDetector.SetInput(blob);
                var detections = _faceDetector.Forward();

                var rows = detections.Size(2);
                Console.WriteLine($"Detecciones encontradas: {rows}");

                for (int i = 0; i < rows; i++)
                {
                    var confidence = detections.At<float>(0, 0, i, 2);
                    Console.WriteLine($"  Detección {i}: confianza = {confidence}");

                    if (confidence < ConfidenceThreshold)
                        continue;

                    var x1 = (int)(detections.At<float>(0, 0, i, 3) * image.Width);
                    var y1 = (int)(detections.At<float>(0, 0, i, 4) * image.Height);
                    var x2 = (int)(detections.At<float>(0, 0, i, 5) * image.Width);
                    var y2 = (int)(detections.At<float>(0, 0, i, 6) * image.Height);

                    faces.Add(new DetectedFace
                    {
                        X = Math.Max(0, x1),
                        Y = Math.Max(0, y1),
                        Width = Math.Min(image.Width - x1, x2 - x1),
                        Height = Math.Min(image.Height - y1, y2 - y1),
                        Confidence = confidence
                    });

                    Console.WriteLine($"  Rostro detectado: ({x1},{y1}) - ({x2},{y2})");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en detección facial: {ex.Message}");
                Console.WriteLine($"Stack: {ex.StackTrace}");
            }

            return faces;
        });
    }

    public async Task<float[]> GenerateEmbeddingAsync(string imagePath, DetectedFace face)
    {
        return await Task.Run(() =>
        {
            var embedding = new float[EmbeddingSize];

            try
            {
                if (_faceEncoder == null)
                    return embedding;

                using var image = Cv2.ImRead(imagePath);
                if (image.Empty())
                    return embedding;

                var faceRect = new Rect(face.X, face.Y, face.Width, face.Height);
                using var faceImage = new Mat(image, faceRect);
                using var resizedFace = new Mat();
                Cv2.Resize(faceImage, resizedFace, new Size(160, 160));

                // Normalizar
                resizedFace.ConvertTo(resizedFace, MatType.CV_32FC3, 1.0 / 255.0);

                // Crear tensor para ONNX
                var inputTensor = new DenseTensor<float>(new[] { 1, 3, 160, 160 });

                for (int y = 0; y < 160; y++)
                {
                    for (int x = 0; x < 160; x++)
                    {
                        var pixel = resizedFace.At<Vec3f>(y, x);
                        inputTensor[0, 0, y, x] = pixel.Item2;
                        inputTensor[0, 1, y, x] = pixel.Item1;
                        inputTensor[0, 2, y, x] = pixel.Item0;
                    }
                }

                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor("input", inputTensor)
                };

                using var results = _faceEncoder.Run(inputs);
                var output = results.First().AsTensor<float>();

                for (int i = 0; i < EmbeddingSize && i < output.Length; i++)
                {
                    embedding[i] = output[i];
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generando embedding: {ex.Message}");
            }

            return embedding;
        });
    }

    public async Task<bool> TestDetectionAsync(string imagePath)
    {
        try
        {
            InitializeModels();

            if (_faceDetector == null)
            {
                Console.WriteLine("FaceDetector no inicializado");
                return false;
            }

            using var image = Cv2.ImRead(imagePath);
            if (image.Empty())
            {
                Console.WriteLine("No se pudo cargar la imagen");
                return false;
            }

            var blob = CvDnn.BlobFromImage(image, 1.0, new Size(300, 300), new Scalar(104, 177, 123));
            _faceDetector.SetInput(blob);
            var detections = _faceDetector.Forward();

            var rows = detections.Size(2);
            Console.WriteLine($"Detecciones encontradas: {rows}");

            for (int i = 0; i < rows; i++)
            {
                var confidence = detections.At<float>(0, 0, i, 2);
                Console.WriteLine($"  Confianza {i}: {confidence}");
            }

            return rows > 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error test: {ex.Message}");
            return false;
        }
    }

    public void Dispose()
    {
        _faceDetector?.Dispose();
        _faceEncoder?.Dispose();
    }
}