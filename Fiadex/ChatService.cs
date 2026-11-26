using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Fiadex;

public sealed class ChatService
{
    private const int MaxToolRounds = 2;
    private const int MaxAnswerLength = 4000;
    private const string GenericErrorMessage =
        "No se pudo consultar el asistente. Verifique que Ollama esté disponible.";
    private const string TimeoutMessage =
        "La consulta tardó demasiado tiempo. Si es la primera vez, Ollama puede estar cargando el modelo; intente de nuevo.";

    private static readonly string DefaultModel =
        Environment.GetEnvironmentVariable("FIADDEX_OLLAMA_MODEL") ?? "qwen2.5:3b";

    private static readonly string DefaultBaseUrl =
        Environment.GetEnvironmentVariable("FIADDEX_OLLAMA_URL") ?? "http://localhost:11434";

    private readonly IFiadexDatabase _database;
    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly string _baseUrl;
    private readonly string _chatEndpoint;
    private readonly List<(string Role, string Text)> _history = [];

    public ChatService(
        IFiadexDatabase database,
        HttpClient? httpClient = null,
        string? model = null,
        string? baseUrl = null)
    {
        _database = database;
        _baseUrl = (baseUrl ?? DefaultBaseUrl).TrimEnd('/');
        ValidateBaseUrl(_baseUrl);

        // El timeout solo se configura en el cliente creado aquí; uno inyectado
        // pertenece a quien lo creó y no debe modificarse.
        if (httpClient is null)
        {
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            _httpClient.BaseAddress = new Uri(_baseUrl + "/");
            _chatEndpoint = "api/chat";
        }
        else
        {
            _httpClient = httpClient;
            if (_httpClient.BaseAddress is not null)
            {
                ValidateBaseUrl(_httpClient.BaseAddress.AbsoluteUri);
                _chatEndpoint = "api/chat";
            }
            else
            {
                _chatEndpoint = _baseUrl + "/api/chat";
            }
        }

        _model = model ?? DefaultModel;
    }

    private static void ValidateBaseUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                "La URL del asistente debe ser una dirección HTTP o HTTPS válida.");
        }

        var host = uri.Host.Trim('[', ']');
        var isLoopback =
            host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            host.Equals("127.0.0.1", StringComparison.Ordinal) ||
            host.Equals("::1", StringComparison.Ordinal) ||
            (IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address));

        if (!isLoopback && uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException(
                "La URL del asistente debe ser HTTPS o apuntar a localhost " +
                "(localhost, 127.0.0.1 o ::1). No se pueden enviar datos de la " +
                "aplicación a un host arbitrario sin cifrar.");
        }
    }

    public async Task<string> AskAsync(string question, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(question);
        _history.Add(("user", question));

        try
        {
            var answer = await RunConversationAsync(question, false, cancellationToken);
            _history.Add(("assistant", answer));
            return answer;
        }
        catch (HttpRequestException)
        {
            return GenericErrorMessage;
        }
        catch (JsonException)
        {
            return GenericErrorMessage;
        }
        catch (TaskCanceledException)
        {
            return TimeoutMessage;
        }
    }

    public async Task<string> SummarizeAsync(CancellationToken cancellationToken = default)
    {
        const string question = "Resume el estado general de préstamos, abonos y saldos.";
        _history.Add(("user", question));

        try
        {
            var answer = await RunConversationAsync(question, true, cancellationToken);
            _history.Add(("assistant", answer));
            return answer;
        }
        catch (HttpRequestException)
        {
            return GenericErrorMessage;
        }
        catch (JsonException)
        {
            return GenericErrorMessage;
        }
        catch (TaskCanceledException)
        {
            return TimeoutMessage;
        }
    }

    public string BuildReadOnlyFacts(string question)
    {
        var dashboard = _database.GetDashboard();
        var loans = _database.GetLoans();
        var payments = _database.GetPayments();
        var builder = new StringBuilder();

        builder.AppendLine(
            $"Resumen: saldo pendiente {dashboard.PendingTotal:C0}; " +
            $"abonos del mes {dashboard.PaymentsThisMonth:C0}; " +
            $"préstamos activos {dashboard.ActiveLoans}; " +
            $"abonos registrados {dashboard.PaymentCount}; " +
            $"campos incompletos {dashboard.MissingFields}.");

        var mentionedName = _database.GetPeople()
            .FirstOrDefault(person =>
                question.Contains(person.Name, StringComparison.OrdinalIgnoreCase));
        if (mentionedName is not null)
        {
            var history = _database.GetPersonHistory()
                .Single(x => x.PersonId == mentionedName.Id);
            builder.AppendLine(
                $"Persona encontrada: {history.PersonName}; " +
                $"prestado {history.LoanTotal:C0}; abonado {history.PaidTotal:C0}; " +
                $"saldo {history.Balance:C0}; préstamos {history.LoanCount}; " +
                $"abonos {history.PaymentCount}.");

            foreach (var payment in _database.GetPaymentHistory(mentionedName.Id))
            {
                builder.AppendLine(
                    $"Abono: {payment.Amount:C0} el {payment.PaymentDate:dd/MM/yyyy}; " +
                    $"préstamo {payment.LoanId}; notas: {payment.Notes}");
            }
        }
        else if (loans.Count > 0)
        {
            foreach (var loan in loans.Take(20))
            {
                builder.AppendLine(
                    $"Préstamo {loan.Loan.Id}: persona {loan.Loan.PersonName}; " +
                    $"monto {loan.Loan.Amount:C0}; abonado {loan.Paid:C0}; " +
                    $"saldo {loan.Balance:C0}; fecha {loan.Loan.LoanDate:dd/MM/yyyy}.");
            }
        }
        else
        {
            builder.AppendLine("No hay préstamos registrados.");
        }

        if (payments.Count == 0)
        {
            builder.AppendLine("No hay abonos registrados.");
        }

        return builder.ToString();
    }

    public void ClearHistory() => _history.Clear();

    private async Task<string> RunConversationAsync(
        string question,
        bool summary,
        CancellationToken cancellationToken)
    {
        var facts = BuildReadOnlyFacts(question);
        var messages = new List<object>
        {
            new { role = "system", content = BuildSystemPrompt(summary, facts) }
        };
        messages.AddRange(_history.TakeLast(8).Select(x =>
            new { role = x.Role, content = x.Text }));

        for (var round = 0; round < MaxToolRounds; round++)
        {
            var reply = await PostChatAsync(messages, !summary, cancellationToken);
            if (reply.ToolCalls is null)
            {
                return TruncateAnswer(ValidateAnswer(reply.Content, facts));
            }

            messages.Add(new
            {
                role = "assistant",
                content = reply.Content,
                tool_calls = reply.ToolCalls.Value
            });

            foreach (var call in reply.ToolCalls.Value.EnumerateArray())
            {
                var function = call.GetProperty("function");
                var name = function.GetProperty("name").GetString() ?? string.Empty;
                var arguments = function.TryGetProperty("arguments", out var args)
                    ? args
                    : default;
                messages.Add(new
                {
                    role = "tool",
                    tool_name = name,
                    content = ExecuteTool(name, arguments)
                });
            }
        }

        return "No se pudo completar la consulta con las herramientas disponibles.";
    }

    private async Task<OllamaReply> PostChatAsync(
        List<object> messages,
        bool useTools,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsJsonAsync(
                _chatEndpoint,
                new
                {
                    model = _model,
                    stream = false,
                    messages,
                    tools = useTools ? BuildToolDefinitions() : null
                },
                cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new HttpRequestException(
                $"Ollama no está disponible en {_baseUrl}. " +
                "Inicie Ollama (icono en la bandeja del sistema) o ejecute 'ollama serve'.",
                ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var detail = (await response.Content.ReadAsStringAsync(cancellationToken)).Trim();
                if (detail.Length > 240)
                {
                    detail = detail[..240];
                }

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new HttpRequestException(
                        $"El modelo '{_model}' no está instalado en Ollama. " +
                        $"Ejecute: ollama pull {_model}");
                }

                var errorMessage = $"Ollama devolvió {(int)response.StatusCode} {response.ReasonPhrase}.";
                if (!string.IsNullOrWhiteSpace(detail))
                {
                    errorMessage += " " + detail;
                }

                throw new HttpRequestException(errorMessage);
            }

            using var document = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(cancellationToken));
            var message = document.RootElement.GetProperty("message");
            var content = message.TryGetProperty("content", out var text)
                ? text.GetString() ?? string.Empty
                : string.Empty;
            JsonElement? toolCalls = null;
            if (useTools &&
                message.TryGetProperty("tool_calls", out var calls) &&
                calls.ValueKind == JsonValueKind.Array &&
                calls.GetArrayLength() > 0)
            {
                toolCalls = calls.Clone();
            }

            return new OllamaReply(content.Trim(), toolCalls);
        }
    }

    private string ExecuteTool(string name, JsonElement arguments)
    {
        switch (name)
        {
            case "get_dashboard":
                var dashboard = _database.GetDashboard();
                return JsonSerializer.Serialize(new
                {
                    saldoPendiente = dashboard.PendingTotal,
                    abonosDelMes = dashboard.PaymentsThisMonth,
                    prestamosActivos = dashboard.ActiveLoans,
                    abonosRegistrados = dashboard.PaymentCount,
                    camposIncompletos = dashboard.MissingFields
                });

            case "get_loans":
                return JsonSerializer.Serialize(_database.GetLoans().Take(20).Select(x => new
                {
                    id = x.Loan.Id,
                    persona = x.Loan.PersonName,
                    monto = x.Loan.Amount,
                    abonado = x.Paid,
                    saldo = x.Balance,
                    fecha = x.Loan.LoanDate.ToString("dd/MM/yyyy")
                }));

            case "get_person_history":
                var personName = arguments.ValueKind == JsonValueKind.Object &&
                                 arguments.TryGetProperty("name", out var nameElement)
                    ? nameElement.GetString() ?? string.Empty
                    : string.Empty;
                var person = _database.GetPeople().FirstOrDefault(p =>
                    p.Name.Contains(personName, StringComparison.OrdinalIgnoreCase));
                if (person is null)
                {
                    return "No se encontró esa persona.";
                }

                var history = _database.GetPersonHistory().Single(x => x.PersonId == person.Id);
                return JsonSerializer.Serialize(new
                {
                    persona = history.PersonName,
                    prestado = history.LoanTotal,
                    abonado = history.PaidTotal,
                    saldo = history.Balance,
                    prestamos = history.LoanCount,
                    abonos = history.PaymentCount
                });

            default:
                return "Función no disponible.";
        }
    }

    private static object[] BuildToolDefinitions() =>
    [
        new
        {
            type = "function",
            function = new
            {
                name = "get_dashboard",
                description = "Devuelve el saldo pendiente, abonos del mes, préstamos activos y abonos registrados. Solo lectura.",
                parameters = new { type = "object", properties = new { } }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "get_person_history",
                description = "Devuelve el historial de préstamos y abonos de una persona indicando su nombre. Solo lectura.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        name = new { type = "string", description = "Nombre de la persona" }
                    },
                    required = new[] { "name" }
                }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "get_loans",
                description = "Devuelve los préstamos con monto, abonado, saldo y fecha. Solo lectura.",
                parameters = new { type = "object", properties = new { } }
            }
        }
    ];

    private static string BuildSystemPrompt(bool summary, string facts) =>
        (summary
            ? "Eres el asistente de Fiadex. Genera un resumen breve en español usando solo los datos proporcionados. No calcules cifras nuevas, no inventes datos y no modifiques registros. Si falta información, indícalo claramente."
            : "Eres el asistente de Fiadex. Solo puedes consultar los datos proporcionados o los obtenidos con las herramientas de solo lectura. No calcules cifras que no estén en los datos, no inventes nombres ni registres cambios. Responde brevemente en español. Si la información no existe, indícalo claramente.")
        + Environment.NewLine + "Datos de solo lectura:" + Environment.NewLine + facts;

    private static string TruncateAnswer(string answer)
    {
        const string suffix = "\n\n[Respuesta truncada por longitud.]";
        return answer.Length <= MaxAnswerLength
            ? answer
            : answer[..(MaxAnswerLength - suffix.Length)] + suffix;
    }

    private static string ValidateAnswer(string answer, string facts)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            return "No se pudo obtener una respuesta.";
        }

        var known = ExtractNumbers(facts);
        var invented = ExtractNumbers(answer)
            .Any(number => number.Length >= 3 && !known.Contains(number));
        return invented
            ? "No pude verificar esas cifras con los datos locales."
            : answer;
    }

    private static HashSet<string> ExtractNumbers(string text) =>
        Regex.Matches(text, @"\d[\d.,]*")
            .Select(match => new string(match.Value.Where(char.IsDigit).ToArray()))
            .Where(number => number.Length > 0)
            .ToHashSet();

    private sealed record OllamaReply(string Content, JsonElement? ToolCalls);
}
