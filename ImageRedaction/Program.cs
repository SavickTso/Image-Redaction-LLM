using OpenAI_API;
using Microsoft.Rest.Azure;
using OpenAI_API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System.Text.Json;
using SixLabors.ImageSharp.Drawing.Processing;

string VISION_KEY = Environment.GetEnvironmentVariable("VISION_KEY") ?? "";
string VISION_ENDPOINT = Environment.GetEnvironmentVariable("VISION_ENDPOINT") ?? "";

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddAntiforgery();
builder.Services.AddSingleton(sp => new OpenAIAPI());
builder.Services.AddSingleton(sp => new ComputerVisionClient(new ApiKeyServiceClientCredentials(VISION_KEY))
{
    Endpoint = VISION_ENDPOINT,
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAntiforgery();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapPost("/prompt", async ([FromBody] PromptRequest req, OpenAIAPI openai) =>
{
    var chat = openai.Chat.CreateConversation();
    chat.Model = Model.GPT4_Turbo;
    chat.AppendSystemMessage($"I am an excellent linguist. The task is to label sensitive information in the given sentence. Please give the output as a JSON array of strings containing the original characters. The user may have additional requests appended in brackets at the end.");
    chat.AppendUserInput("Hey Mason, my name is cao I live in bunkyo-ku nezu. my phone number is 1234567, and my email address is djhf@gmail.com");
    chat.AppendExampleChatbotOutput("""["Mason", "cao", "bunkyo-ku nezu", "1234567", "djhf@gmail.com"]""");
    chat.AppendUserInput("Hey Tom, this is Toby. (Please do not mark Tom as sensitive)");
    chat.AppendExampleChatbotOutput("""["Toby"]""");
    chat.AppendUserInput(req.Data);
    Console.WriteLine(req.Data);
    return await chat.GetResponseFromChatbotAsync();
})
.WithName("GetRedaction")
.WithOpenApi();

app.MapPost("/ocr", async ([FromBody] ImageRequest ocr, ComputerVisionClient cv) =>
{
    var data = Convert.FromBase64String(ocr.Data);
    var textHeaders = await cv.ReadInStreamAsync(new MemoryStream(data));
    string operationLocation = textHeaders.OperationLocation;
    Thread.Sleep(2000);

    // Retrieve the URI where the extracted text will be stored from the Operation-Location header.
    // We only need the ID and not the full URL
    const int numberOfCharsInOperationId = 36;
    string operationId = operationLocation.Substring(operationLocation.Length - numberOfCharsInOperationId);

    // Extract the text
    ReadOperationResult results;
    do
    {
        results = await cv.GetReadResultAsync(Guid.Parse(operationId));
    }
    while (results.Status == OperationStatusCodes.Running || results.Status == OperationStatusCodes.NotStarted);

    return results;
})
.WithName("GetOCR")
.WithOpenApi();

app.MapPost("/process", async ([FromBody] RedactRequest req, ComputerVisionClient cv, OpenAIAPI openai) =>
{
    var data = Convert.FromBase64String(req.Data);
    var textHeaders = await cv.ReadInStreamAsync(new MemoryStream(data));
    string operationLocation = textHeaders.OperationLocation;
    Thread.Sleep(2000);

    // Retrieve the URI where the extracted text will be stored from the Operation-Location header.
    // We only need the ID and not the full URL
    const int numberOfCharsInOperationId = 36;
    string operationId = operationLocation.Substring(operationLocation.Length - numberOfCharsInOperationId);

    // Extract the text
    ReadOperationResult results;
    do
    {
        results = await cv.GetReadResultAsync(Guid.Parse(operationId));
    }
    while (results.Status == OperationStatusCodes.Running || results.Status == OperationStatusCodes.NotStarted);

    var chat = openai.Chat.CreateConversation();
    chat.Model = Model.GPT4_Turbo;
    chat.AppendSystemMessage($"I am an excellent linguist. The task is to label sensitive information in the given sentence. Please give the output as a JSON array of strings containing the original characters. The user may have additional requests appended in brackets on a separate line at the end.");
    chat.AppendUserInput("Hey Mason, my name is cao I live in bunkyo-ku nezu. my phone number is 1234567, and my email address is djhf@gmail.com");
    chat.AppendExampleChatbotOutput("""["Mason", "cao", "bunkyo-ku nezu", "1234567", "djhf@gmail.com"]""");
    chat.AppendUserInput("Hey Tom, this is Toby. (Please do not mark Tom as sensitive)");
    chat.AppendExampleChatbotOutput("""["Toby"]""");
    var chatReq = string.Join("\n\n", results.AnalyzeResult.ReadResults.Select(r => string.Join('\n', r.Lines.Select(l => l.Text)))) + $"\n[{string.Join(". ", req.Prompts)}]";
    chat.AppendUserInput(chatReq);
    var prompt = await chat.GetResponseFromChatbotAsync();
    Console.WriteLine("======");
    foreach (var page in results.AnalyzeResult.ReadResults)
    {
        foreach (var line in page.Lines)
        {
            Console.WriteLine(line.Text);
        }
    }
    Console.WriteLine("======");
    Console.WriteLine(chatReq);
    Console.WriteLine("======");
    Console.WriteLine(prompt);
    Console.WriteLine("======");

    if (prompt.StartsWith("```json") && prompt.EndsWith("```"))
    {
        prompt = prompt.Substring("```json".Length, prompt.Length - "```json".Length - "```".Length);
    }
    var json = JsonSerializer.Deserialize<IEnumerable<string>>(prompt);
    if (json == null) return new RedactResponse
    {
        Image = req.Data,
    };

    using var image = Image.Load(data);
    foreach (var page in results.AnalyzeResult.ReadResults)
    {
        foreach (var line in page.Lines)
        {
            if (json.Any(w => Distance(w, line.Text) < 0.1))
            {
                var topLeft = new Point((int)(line.BoundingBox[0] ?? 0), (int)(line.BoundingBox[1] ?? 0));
                var topRight = new Point((int)(line.BoundingBox[2] ?? 0), (int)(line.BoundingBox[3] ?? 0));
                var bottomRight = new Point((int)(line.BoundingBox[4] ?? 0), (int)(line.BoundingBox[5] ?? 0));
                var bottomLeft = new Point((int)(line.BoundingBox[6] ?? 0), (int)(line.BoundingBox[7] ?? 0));
                image.Mutate(x => x.Fill(Color.Black, new Rectangle(topLeft, new Size(bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y))));
            }
            foreach (var word in line.Words)
            {
                if (json.Any(w => Distance(w, word.Text) < 0.1))
                {
                    var topLeft = new Point((int)(word.BoundingBox[0] ?? 0), (int)(word.BoundingBox[1] ?? 0));
                    var topRight = new Point((int)(word.BoundingBox[2] ?? 0), (int)(word.BoundingBox[3] ?? 0));
                    var bottomRight = new Point((int)(word.BoundingBox[4] ?? 0), (int)(word.BoundingBox[5] ?? 0));
                    var bottomLeft = new Point((int)(word.BoundingBox[6] ?? 0), (int)(word.BoundingBox[7] ?? 0));
                    image.Mutate(x => x.Fill(Color.Black, new Rectangle(topLeft, new Size(bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y))));
                }
            }
        }
    }

    var outputStream = new MemoryStream();
    image.SaveAsJpeg(outputStream);
    var imageBase64 = Convert.ToBase64String(outputStream.ToArray());
    return new RedactResponse
    {
        Image = imageBase64,
    };
})
.WithName("ImageRedaction")
.WithOpenApi();

app.Run();


double Distance(string s, string t)
{
    int n = s.Length;
    int m = t.Length;
    int[,] d = new int[n + 1, m + 1];

    // Verify arguments.
    if (n == 0)
    {
        return m;
    }

    if (m == 0)
    {
        return n;
    }

    // Initialize arrays.
    for (int i = 0; i <= n; d[i, 0] = i++)
    {
    }

    for (int j = 0; j <= m; d[0, j] = j++)
    {
    }

    // Begin looping.
    for (int i = 1; i <= n; i++)
    {
        for (int j = 1; j <= m; j++)
        {
            // Compute cost.
            int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
            d[i, j] = Math.Min(
            Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
            d[i - 1, j - 1] + cost);
        }
    }
    // Return cost.
    return (double)d[n, m] / Math.Max(s.Length, t.Length);
}

record PromptRequest
{
    public required string Data { get; init; }
};

record ImageRequest
{
    public required string Data { get; init; }
}

record RedactRequest
{
    public required string Data { get; init; }
    public required IEnumerable<string> Prompts { get; init; }
}

record RedactResponse
{
    public required string Image { get; init; }
}
