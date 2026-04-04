using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Woola.PhotoManager.Common.Services;

public class TextEmbeddingService : IDisposable
{
    private InferenceSession? _session;
    private readonly int _embeddingSize = 384;
    private bool _isInitialized;
    private bool _modelAvailable;

    public TextEmbeddingService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var modelsDir = Path.Combine(appData, "Woola", "Models");
        Directory.CreateDirectory(modelsDir);

        var modelPath = Path.Combine(modelsDir, "all-MiniLM-L6-v2.onnx");

        if (!File.Exists(modelPath))
        {
            modelPath = Path.Combine(modelsDir, "model_quantized.onnx");
        }

        if (File.Exists(modelPath))
        {
            try
            {
                _session = new InferenceSession(modelPath);
                _isInitialized = true;
                _modelAvailable = true;
                Console.WriteLine($"✅ Modelo de embeddings cargado: {modelPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error cargando modelo: {ex.Message}");
                _modelAvailable = false;
            }
        }
        else
        {
            Console.WriteLine($"⚠️ Modelo de embeddings no encontrado en: {modelPath}");
            Console.WriteLine($"   La búsqueda semántica no estará disponible.");
            Console.WriteLine($"   Descarga el modelo desde: https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/onnx/model_quantized.onnx");
            _modelAvailable = false;
        }
    }

    public bool IsAvailable => _modelAvailable;

    public float[] GenerateEmbedding(string text)
    {
        if (!_modelAvailable || _session == null)
        {
            // Devolver embedding vacío en lugar de lanzar excepción
            return new float[_embeddingSize];
        }

        try
        {
            var tokens = TokenizeSimple(text);
            var inputTensor = new DenseTensor<long>(new[] { 1, tokens.Length });
            for (int i = 0; i < tokens.Length; i++)
                inputTensor[0, i] = tokens[i];

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input_ids", inputTensor)
            };

            using var results = _session.Run(inputs);
            var output = results.First().AsTensor<float>();

            var embedding = new float[_embeddingSize];
            for (int i = 0; i < _embeddingSize && i < output.Length; i++)
                embedding[i] = output[i];

            var norm = (float)Math.Sqrt(embedding.Sum(x => x * x));
            if (norm > 0)
            {
                for (int i = 0; i < embedding.Length; i++)
                    embedding[i] /= norm;
            }

            return embedding;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error generando embedding: {ex.Message}");
            return new float[_embeddingSize];
        }
    }

    private int[] TokenizeSimple(string text)
    {
        text = text.ToLower().Trim();
        var words = text.Split(new[] { ' ', '.', ',', ';', ':', '!', '?', '-', '_', '(', ')' },
                               StringSplitOptions.RemoveEmptyEntries);

        var tokens = new List<int>();
        tokens.Add(101); // [CLS]

        foreach (var word in words)
        {
            if (string.IsNullOrWhiteSpace(word)) continue;
            var token = Math.Abs(word.GetHashCode()) % 30000 + 1000;
            tokens.Add(token);
        }

        tokens.Add(102); // [SEP]

        if (tokens.Count > 128)
            tokens = tokens.Take(128).ToList();

        return tokens.ToArray();
    }

    public float CosineSimilarity(float[] a, float[] b)
    {
        float dot = 0;
        for (int i = 0; i < a.Length; i++)
            dot += a[i] * b[i];
        return dot;
    }

    public void Dispose()
    {
        _session?.Dispose();
    }
}