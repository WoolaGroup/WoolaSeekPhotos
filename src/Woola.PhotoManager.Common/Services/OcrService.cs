using Tesseract;
using Woola.PhotoManager.Common.Models;

namespace Woola.PhotoManager.Common.Services;

public class OcrService : IOcrService, IDisposable
{
    private readonly TesseractEngine _engine;
    private readonly string _tessDataPath;
    private bool _isInitialized;

    public OcrService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _tessDataPath = Path.Combine(appData, "Woola", "TessData");
        Directory.CreateDirectory(_tessDataPath);

        try
        {
            _engine = new TesseractEngine(_tessDataPath, "spa+eng", EngineMode.Default);
            _engine.SetVariable("tessedit_char_whitelist", "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyzÁÉÍÓÚÜÑáéíóúüñ0123456789 .,;:-()");
            _isInitialized = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error inicializando Tesseract: {ex.Message}");
            _isInitialized = false;
            throw;
        }
    }

    public async Task<bool> IsAvailable()
    {
        return await Task.Run(() => _isInitialized && File.Exists(Path.Combine(_tessDataPath, "spa.traineddata")));
    }

    public async Task<OcrResult> ExtractTextAsync(string imagePath)
    {
        return await Task.Run(() =>
        {
            var result = new OcrResult { Success = false };

            try
            {
                if (!_isInitialized)
                {
                    result.Text = "OCR no inicializado";
                    return result;
                }

                using var img = Pix.LoadFromFile(imagePath);
                using var page = _engine.Process(img);

                var text = page.GetText();
                var meanConfidence = page.GetMeanConfidence();

                result.Text = text.Trim();
                result.Confidence = meanConfidence;
                result.Success = !string.IsNullOrWhiteSpace(result.Text);

                // Extraer palabras individuales (sin coordenadas)
                using var iterator = page.GetIterator();
                if (iterator != null)
                {
                    do
                    {
                        var wordText = iterator.GetText(PageIteratorLevel.Word);
                        if (!string.IsNullOrWhiteSpace(wordText))
                        {
                            var confidence = iterator.GetConfidence(PageIteratorLevel.Word);

                            result.Words.Add(new OcrWord
                            {
                                Text = wordText.Trim(),
                                Confidence = confidence / 100.0
                            });
                        }
                    } while (iterator.Next(PageIteratorLevel.Word));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error OCR: {ex.Message}");
                result.Text = $"Error: {ex.Message}";
            }

            return result;
        });
    }


    private string DetectDocumentType(string text)
    {
        text = text.ToLower();

        if (text.Contains("factura") || text.Contains("invoice") || (text.Contains("total") && text.Contains("iva")))
            return "factura";

        if (text.Contains("recibo") || text.Contains("pago") || text.Contains("cantidad recibida"))
            return "recibo";

        if (text.Contains("identificación") || text.Contains("cedula") || text.Contains("ine") || text.Contains("pasaporte"))
            return "identificacion";

        if (text.Contains("contrato") || text.Contains("cláusula") || text.Contains("acuerdo"))
            return "contrato";

        if (text.Contains("curriculum") || text.Contains("cv") || text.Contains("experiencia laboral"))
            return "curriculum";

        if (text.Contains("receta médica") || text.Contains("diagnóstico") || text.Contains("paciente"))
            return "documento_medico";

        return "documento_general";
    }

    private List<string> ExtractImportantNumbers(string text)
    {
        var numbers = new List<string>();

        // Montos con $ o MXN
        var amountRegex = new System.Text.RegularExpressions.Regex(@"[\$\s]*(\d{1,3}(?:[.,]\d{3})*(?:[.,]\d{2})?)\s*(MXN|USD|€)?");
        var amounts = amountRegex.Matches(text);
        foreach (System.Text.RegularExpressions.Match match in amounts)
        {
            if (match.Groups[1].Success && match.Groups[1].Value.Length > 2)
            {
                numbers.Add($"monto_{match.Groups[1].Value.Replace(",", "").Replace(".", "")}");
            }
        }

        // Números de teléfono (10 dígitos)
        var phoneRegex = new System.Text.RegularExpressions.Regex(@"\b(\d{2}[-.\s]?\d{4}[-.\s]?\d{4}|\d{10})\b");
        var phones = phoneRegex.Matches(text);
        foreach (System.Text.RegularExpressions.Match match in phones)
        {
            numbers.Add("contiene_telefono");
            break;
        }

        // Códigos postales (5 dígitos)
        var zipRegex = new System.Text.RegularExpressions.Regex(@"\b\d{5}\b");
        if (zipRegex.IsMatch(text))
            numbers.Add("contiene_codigo_postal");

        // RFC (México)
        var rfcRegex = new System.Text.RegularExpressions.Regex(@"\b[A-ZÑ&]{3,4}\d{6}[A-Z0-9]{3}\b");
        if (rfcRegex.IsMatch(text))
            numbers.Add("contiene_rfc");

        return numbers;
    }
    public void Dispose()
    {
        _engine?.Dispose();
    }
}