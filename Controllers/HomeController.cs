using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using cover_letter.Models;
using Mscc.GenerativeAI;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using System.Text;


namespace cover_letter.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View(new HomeViewModel());
    }


    private string ExtractTextFromPdf(string pdfPath)
    {
        using (PdfReader reader = new PdfReader(pdfPath))
        using (PdfDocument pdfDoc = new PdfDocument(reader))
        {
            StringBuilder text = new StringBuilder();
            for (int i = 1; i <= pdfDoc.GetNumberOfPages(); i++)
            {
                text.Append(PdfTextExtractor.GetTextFromPage(pdfDoc.GetPage(i)));
            }
            return text.ToString();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(IFormFile resume, IFormFile open_position, string language)
    {
        // Validasi apakah file diunggah
        if (resume == null || open_position == null)
        {
            ModelState.AddModelError(string.Empty, "Please upload both files.");
            return View(new HomeViewModel());
        }

        // Validasi format file resume harus PDF
        if (resume.ContentType != "application/pdf")
        {
            ModelState.AddModelError(string.Empty, "Resume must be a PDF file.");
            return View(new HomeViewModel());
        }

        // Validasi format file untuk open position image harus berupa gambar (jpg/png)
        var validImageTypes = new[] { "image/jpeg", "image/png" };
        if (!validImageTypes.Contains(open_position.ContentType))
        {
            ModelState.AddModelError(string.Empty, "Open position must be a JPG or PNG image.");
            return View(new HomeViewModel());
        }

        string coverLetter;
        // Set the path for the wwwroot/upload directory
        var uploadDirectory = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");

        // Ensure the upload directory exists
        if (!Directory.Exists(uploadDirectory))
        {
            Directory.CreateDirectory(uploadDirectory);
        }

        // Save the uploaded files to wwwroot/upload
        var resumePath = Path.Combine(uploadDirectory, resume.FileName);
        var imagePath = Path.Combine(uploadDirectory, open_position.FileName);

        try
        {
            // Save resume file
            using (var stream = new FileStream(resumePath, FileMode.Create))
            {
                await resume.CopyToAsync(stream);
            }

            // Save open position image file
            using (var stream = new FileStream(imagePath, FileMode.Create))
            {
                await open_position.CopyToAsync(stream);
            }

            // Prepare the prompt based on the selected language
            string prompt = $"Craft a cover letter in {language} for the uploaded resume and job position image.";

            // Call the API to generate the cover letter
            coverLetter = await CallGeminiApi(prompt, resumePath, imagePath);
        }
        catch (Exception ex)
        {
            coverLetter = $"An error occurred: {ex.Message}";
        }

        // Create a model to pass back to the view
        var model = new HomeViewModel
        {
            CoverLetter = coverLetter
        };

        // Optionally, delete temporary files after processing
        System.IO.File.Delete(resumePath);
        System.IO.File.Delete(imagePath);

        return View(model);
    }


    private async Task<string> CallGeminiApi(string prompt, string resumePath, string imagePath)
    {
        var googleAI = new GoogleAI(apiKey: Environment.GetEnvironmentVariable("GEMINI_API_KEY"));
        var model = googleAI.GenerativeModel(model: Model.Gemini15Pro);

        try
        {
            // Extract text from the resume PDF
            string resumeText = ExtractTextFromPdf(resumePath);

            // Create a prompt including the extracted text
            string fullPrompt = $"{prompt}\n\nResume Content:\n{resumeText}";
            // Generate the content
            var request = new GenerateContentRequest(fullPrompt);
            // await request.AddMedia(resumePath);
            await request.AddMedia(imagePath);

            // Generate the content using the request
            var response = await model.GenerateContent(request);

            // Return the generated cover letter text
            return response?.Text ?? "No response text received.";
        }
        catch (Exception ex)
        {
            return $"An error occurred: {ex.Message}";
        }
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
