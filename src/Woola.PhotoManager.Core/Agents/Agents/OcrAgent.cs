using System.Text.RegularExpressions;
using Woola.PhotoManager.Common.Services;
using Woola.PhotoManager.Domain.Entities;

namespace Woola.PhotoManager.Core.Agents.Agents;

public class OcrAgent : IAgent
{
    private readonly IOcrService _ocrService;

    private readonly HashSet<string> _garbageWords = new()
    {
        // Basura común de OCR
        "eues", "ejoom", "oe", "ad", "et", "est", "del", "los", "las", "con", "sin",
        "por", "para", "como", "esta", "este", "esto", "estos", "estas", "pero",
        "sino", "aunque", "porque", "entonces", "asi", "tambien", "cuando", "donde",
        "mientras", "durante", "mediante", "través", "tanto", "pues", "bajo", "cabe",
        "versus", "via", "fue", "ser", "son", "han", "sido", "puede", "pueden",
        "tiene", "tienen", "hace", "hacen", "dice", "dicen", "vez", "veces", "lugar",
        "gente", "parte", "gran", "mayor", "menor", "nuevo", "viejo", "gran", "tras"
    };

    public string Name => "OcrAgent";
    public string Description => "Extrae texto de imágenes usando OCR";
    public int Priority => 4;
    public bool IsEnabled { get; set; } = true;

    public OcrAgent(IOcrService ocrService)
    {
        _ocrService = ocrService;
    }

    public bool CanProcess(Photo photo)
    {
        var extensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".tiff" };
        var ext = Path.GetExtension(photo.Path).ToLower();
        return extensions.Contains(ext) && File.Exists(photo.Path);
    }

    public async Task<AgentResult> ExecuteAsync(Photo photo, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.Now;
        var result = new AgentResult { AgentName = Name, Success = true };

        try
        {
            if (!await _ocrService.IsAvailable())
            {
                result.Success = false;
                result.ErrorMessage = "OCR no disponible. Descargue los datos de lenguaje.";
                return result;
            }

            var ocrResult = await _ocrService.ExtractTextAsync(photo.Path);

            if (ocrResult.Success && !string.IsNullOrWhiteSpace(ocrResult.Text))
            {
                var text = ocrResult.Text;
                var cleanedText = CleanOcrText(text);

                // Tag: contiene texto
                result.Tags.Add(new AgentTag
                {
                    Name = "contiene_texto",
                    Category = "Feature",
                    Confidence = Math.Min(ocrResult.Confidence, 1.0),
                    Source = Name
                });

                // Detectar tipo de documento
                var docType = DetectDocumentType(text);
                result.Tags.Add(new AgentTag
                {
                    Name = docType,
                    Category = "Document",
                    Confidence = 0.8,
                    Source = Name
                });

                // Extraer números importantes
                var importantNumbers = ExtractImportantNumbers(text);
                foreach (var number in importantNumbers)
                {
                    result.Tags.Add(new AgentTag
                    {
                        Name = number,
                        Category = "Number",
                        Confidence = 0.7,
                        Source = Name
                    });
                }

                // Extraer palabras clave significativas (del texto limpiado)
                if (!string.IsNullOrWhiteSpace(cleanedText))
                {
                    var words = Regex.Split(cleanedText, @"\s+")
                        .Where(w => w.Length >= 4 && w.Length <= 15)
                        .Select(w => w.ToLowerInvariant())
                        .Distinct()
                        .Take(15);

                    foreach (var word in words)
                    {
                        // Saltar palabras basura
                        if (_garbageWords.Contains(word))
                            continue;

                        // Saltar palabras sin vocales suficientes
                        var vowelCount = word.Count(c => "aeiouáéíóúü".Contains(c));
                        if (vowelCount < 2 && word.Length > 4)
                            continue;

                        // Saltar palabras que son solo números
                        if (Regex.IsMatch(word, @"^\d+$"))
                            continue;

                        result.Tags.Add(new AgentTag
                        {
                            Name = $"texto_{word}",
                            Category = "Keyword",
                            Confidence = 0.6,
                            Source = Name
                        });
                    }
                }

                // Si la confianza es alta y hay varios tags, marcar como texto_confiable
                if (ocrResult.Confidence > 0.8 && result.Tags.Count > 2)
                {
                    result.Tags.Add(new AgentTag
                    {
                        Name = "texto_confiable",
                        Category = "Quality",
                        Confidence = ocrResult.Confidence,
                        Source = Name
                    });
                }
            }

            result.ProcessingTimeMs = (DateTime.Now - startTime).TotalMilliseconds;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    private string CleanOcrText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        // Eliminar caracteres no deseados
        var cleaned = Regex.Replace(text, @"[^\w\sáéíóúüñÁÉÍÓÚÜÑ]", " ");

        // Eliminar múltiples espacios
        cleaned = Regex.Replace(cleaned, @"\s+", " ");

        // Eliminar palabras de 1 o 2 caracteres
        cleaned = Regex.Replace(cleaned, @"\b\w{1,2}\b", "");

        // Eliminar palabras que son solo números
        cleaned = Regex.Replace(cleaned, @"\b\d+\b", "");

        // Normalizar a minúsculas
        cleaned = cleaned.ToLower().Trim();

        return cleaned;
    }

    private string DetectDocumentType(string text)
    {
        text = text.ToLower();

        if (text.Contains("factura") || text.Contains("invoice") || (text.Contains("total") && text.Contains("iva")))
            return "tipo_factura";

        if (text.Contains("recibo") || text.Contains("pago") || text.Contains("cantidad recibida"))
            return "tipo_recibo";

        if (text.Contains("identificación") || text.Contains("cedula") || text.Contains("ine") || text.Contains("pasaporte"))
            return "tipo_identificacion";

        if (text.Contains("contrato") || text.Contains("cláusula") || text.Contains("acuerdo"))
            return "tipo_contrato";

        if (text.Contains("curriculum") || text.Contains("cv") || text.Contains("experiencia laboral"))
            return "tipo_curriculum";

        if (text.Contains("receta médica") || text.Contains("diagnóstico") || text.Contains("paciente"))
            return "tipo_medico";

        if (text.Contains("cfdi") || (text.Contains("rfc") && text.Contains("uso cfdi")))
            return "tipo_cfdi";

        if (text.Contains("tarjeta") && text.Contains("crédito"))
            return "tipo_tarjeta";

        if (text.Contains("menú") || text.Contains("platillo") || text.Contains("restaurante"))
            return "tipo_menu";

        if (text.Contains("presupuesto") || text.Contains("cotización"))
            return "tipo_cotizacion";

        return "tipo_documento";
    }

    private List<string> ExtractImportantNumbers(string text)
    {
        var numbers = new List<string>();

        // Montos con $ o MXN
        var amountRegex = new Regex(@"[\$\s]*(\d{1,3}(?:[.,]\d{3})*(?:[.,]\d{2})?)\s*(MXN|USD|€)?");
        var amounts = amountRegex.Matches(text);
        foreach (Match match in amounts)
        {
            if (match.Groups[1].Success && match.Groups[1].Value.Length > 2)
            {
                var cleanNumber = match.Groups[1].Value.Replace(",", "").Replace(".", "");
                if (cleanNumber.Length >= 3 && cleanNumber.Length <= 10)
                {
                    numbers.Add($"monto_{cleanNumber}");
                    break;
                }
            }
        }

        // Números de teléfono
        var phoneRegex = new Regex(@"\b(\d{2}[-.\s]?\d{4}[-.\s]?\d{4}|\d{10})\b");
        if (phoneRegex.IsMatch(text))
            numbers.Add("contiene_telefono");

        // Correos electrónicos
        var emailRegex = new Regex(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b");
        if (emailRegex.IsMatch(text))
            numbers.Add("contiene_email");

        // Fechas (dd/mm/yyyy)
        var dateRegex = new Regex(@"\b\d{1,2}[/-]\d{1,2}[/-]\d{2,4}\b");
        if (dateRegex.IsMatch(text))
            numbers.Add("contiene_fecha");

        // Códigos postales (5 dígitos)
        var zipRegex = new Regex(@"\b\d{5}\b");
        if (zipRegex.IsMatch(text))
            numbers.Add("contiene_codigo_postal");

        // RFC México (3-4 letras + 6 dígitos + 3 dígitos/letras)
        var rfcRegex = new Regex(@"\b[A-ZÑ&]{3,4}\d{6}[A-Z0-9]{3}\b");
        if (rfcRegex.IsMatch(text))
            numbers.Add("contiene_rfc");

        // CURP México (4 letras + 6 dígitos + 6 letras + 2 dígitos/letras)
        var curpRegex = new Regex(@"\b[A-Z]{4}\d{6}[A-Z]{6}[A-Z0-9]{2}\b");
        if (curpRegex.IsMatch(text))
            numbers.Add("contiene_curp");

        return numbers.Distinct().ToList();
    }
}