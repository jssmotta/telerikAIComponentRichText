using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Telerik.WinControls.UI;
using Telerik.WinForms.Documents.FormatProviders.OpenXml.Docx;
using Telerik.WinForms.Documents.FormatProviders.Txt;
using Telerik.WinForms.Documents.Model;
using Telerik.WinForms.Documents.RichTextBoxCommands;
using Timer = System.Windows.Forms.Timer;

namespace SmartAIComponents
{
    public partial class MainForm : RadForm
    {
        private Timer timer;

        public string[] Chunks { get; set; }

        public MainForm()
        {
            InitializeComponent();

            DocxFormatProvider provider = new DocxFormatProvider();
            this.radRichTextSmartEditor.Document =
                provider.Import(File.ReadAllBytes(@"..\..\..\..\SampleData\New_App_specification.docx"));

            this.radRichTextSmartEditor.RichTextBoxElement.PreviewEditorKeyDown +=
                this.RichTextBoxElement_PreviewEditorKeyDown;
            this.radRichTextSmartEditor.CommandExecuted += this.RadRichTextEditor_CommandExecuted;

            this.timer = new Timer();
            this.timer.Interval = 2000;
            this.timer.Tick += Timer_Tick;

            // The document could be stored and updated by other memebers teams of the company

            var allTextContent = CopyTextFromDocx(@"..\..\..\..\SampleData\Context.docx");

            Chunks = allTextContent.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Distinct().ToArray();
        }

        //// Extract text from a .docx file
        public static string CopyTextFromDocx(string file)
        {
            var docxFormatProvider = new DocxFormatProvider();
            using var input = File.OpenRead(file);
            var document = docxFormatProvider.Import(input);
            var txtFormatProvider = new TxtFormatProvider();
            return txtFormatProvider.Export(document);
        }
        private void Timer_Tick(object? sender, EventArgs e)
        {
            timer.Stop();

            string question = this.GetCurrentText();
            if (string.IsNullOrEmpty(question))
            {
                return;
            }

            string answer = this.AnswerQuestion(question);
            AppendText(this.radRichTextSmartEditor, answer);
        }

        private string GetCurrentText()
        {
            Paragraph paragraph = this.radRichTextSmartEditor.Document.CaretPosition.GetCurrentParagraph();
            StringBuilder sb = new StringBuilder();

            if (paragraph != null)
            {
                foreach (Span span in paragraph.EnumerateChildrenOfType<Span>())
                {
                    sb.Append(span.Text);
                }
            }

            return sb.ToString();
        }

        private string AnswerQuestion(string question)
        {
            // Use all document content:

            string allContext = string.Join(" ", Chunks);

            var answer = CallOpenAIApi("You are a helpful assistant. Use the provided context to answer the user question. Context: " + allContext, question);

            return answer;
        }

        /*
        CallOpenAIApi - With Azure SDK
private static string CallOpenAIApi(string systemPrompt, string message)
{
    // Add your key and endpoint  to use the OpenAI API.
    OpenAIClient client = new OpenAIClient(
        new Uri("AZURE_ENDPOINT"),
        new AzureKeyCredential("AZURE_KEY"));

    ChatCompletionsOptions chatCompletionsOptions = new ChatCompletionsOptions()
    {
        DeploymentName = "DeploymentName",
        Messages =
        {
            new ChatRequestSystemMessage(systemPrompt),
            new ChatRequestUserMessage(message),
        }
    };

    Response<ChatCompletions> response = client.GetChatCompletions(chatCompletionsOptions);
    ChatResponseMessage responseMessage = response.Value.Choices[0].Message;

    return responseMessage.Content;
}
        */

        private static string CallOpenAIApi(string systemPrompt, string message)
        {
            // Your OpenAI API key
            var apiKey = Environment.GetEnvironmentVariable("API_KEY_OPENAI") ??
                            throw new Exception("Environment API Key is Missing");

            // OpenAI API endpoint for chat completions
            var endpoint = "https://api.openai.com/v1/chat/completions";

            using var httpClient = new HttpClient();

            // Set the authorization header with your API key
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            // Prepare the request body
            var requestBody = new
            {
                model = "gpt-3.5-turbo", // Specify the model you want to use
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = message }
                }
            };

            // Serialize the request body to JSON
            var jsonRequestBody = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonRequestBody, System.Text.Encoding.UTF8, "application/json");

            // Prepare the request message
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = content };

            // Send the POST request to the OpenAI API
            var response = httpClient.Send(requestMessage);

            // Ensure the request was successful
            response.EnsureSuccessStatusCode();

            // Read and parse the response body
            var responseBody = response.Content.ReadAsStringAsync().Result; // Use .Result to synchronously wait on the task
            using var doc = JsonDocument.Parse(responseBody);
            // Extract the content of the first response message
            var responseMessage = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();

            return responseMessage ?? throw new Exception("Error reading response.");
        }


        private static void AppendText(RadRichTextEditor editor, string text)
        {
            Span s = new Span(text);
            s.ForeColor = Color.DimGray;

            editor.InsertInline(s);
            editor.Document.CaretPosition.MoveToDocumentElementStart(s);
        }

        private void RadRichTextEditor_CommandExecuted(object? sender, CommandExecutedEventArgs e)
        {
            if (e.Command is InsertTextCommand)
            {
                this.timer.Stop();
                this.timer.Start();
            }
        }

        private void RichTextBoxElement_PreviewEditorKeyDown(object sender, Telerik.WinForms.Documents.PreviewEditorKeyEventArgs e)
        {
            if (e.OriginalArgs.KeyCode == Keys.Tab)
            {
                var p = this.radRichTextSmartEditor.Document.CaretPosition.GetCurrentParagraph();
                var span = p.Inlines.OfType<Span>().Where(s => Color.DimGray == s.ForeColor).LastOrDefault();
                if (span != null)
                {
                    e.SuppressDefaultAction = true;

                    span.ForeColor = Color.Black;
                    this.radRichTextSmartEditor.UpdateEditorLayout();
                    this.radRichTextSmartEditor.Document.CaretPosition.MoveToDocumentElementEnd(p);
                }
            }
        }
    }
}
