using Assignment_3.Data;
using Assignment_3.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;
using System.IO;

namespace Assignment_3.Controllers
{
    public class MoviesController : Controller
    {
        private readonly Assignment_3Context _context;

        private const int MaxInputLength = 512;

        public MoviesController(Assignment_3Context context)
        {
            _context = context;
        }

        // GET: Movies
        public async Task<IActionResult> Index()
        {
            return View(await _context.Movie.ToListAsync());
        }

        // GET: Movies/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var movie = await _context.Movie
                .FirstOrDefaultAsync(m => m.id == id);
            if (movie == null)
            {
                return NotFound();
            }

            var sentiment = await GetSentimentDataAsync(movie.title ?? "");
            ViewBag.RedditComments = sentiment.Comments;
            ViewBag.SentimentScores = sentiment.Scores;
            ViewBag.AverageSentimentScore = sentiment.AverageScore;

            return View(movie);
        }

        // GET: Movies/Create
        public IActionResult Create()
        {
            return View();
        }
        
        /// <summary>
        /// Returns Reddit comments, per-comment sentiment scores, and average score for use in the Details view.
        /// </summary>
        public async Task<SentimentResult> GetSentimentDataAsync(string title)
        {
            var result = new SentimentResult();
            var httpClient = new HttpClient();

            var url = "https://router.huggingface.co/hf-inference/models/distilbert/distilbert-base-uncased-finetuned-sst-2-english";

            var apiKeyPath = Path.Combine(Directory.GetCurrentDirectory(), "hf_api_key.txt");
            var apiKey = System.IO.File.Exists(apiKeyPath)
                ? System.IO.File.ReadAllText(apiKeyPath).Trim()
                : Environment.GetEnvironmentVariable("HF_API_KEY");

            List<string> textToExamine = await SearchRedditAsync(title);

            double totalScore = 0;
            int validResponses = 0;

            foreach (var post in textToExamine)
            {
                var data = new { inputs = new[] { post } };
                var json = JsonSerializer.Serialize(data);

                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri(url),
                    Headers = { { "Authorization", $"Bearer {apiKey}" } },
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };

                var response = await httpClient.SendAsync(request);
                var responseString = await response.Content.ReadAsStringAsync();

                try
                {
                    List<SentimentResponse>? firstLevel = null;

                    var nested = JsonSerializer.Deserialize<List<List<SentimentResponse>>>(responseString);
                    if (nested != null && nested.Count > 0)
                    {
                        firstLevel = nested[0];
                    }

                    if (firstLevel == null)
                    {
                        firstLevel = JsonSerializer.Deserialize<List<SentimentResponse>>(responseString);
                    }

                    if (firstLevel != null && firstLevel.Count > 0)
                    {
                        var sentimentResult = firstLevel[0];
                        var confidence = (double)sentimentResult.Score;

                        if (sentimentResult.Label == "NEGATIVE")
                        {
                            confidence *= -1;
                        }

                        result.Comments.Add(post);
                        result.Scores.Add(confidence);
                        totalScore += confidence;
                        validResponses += 1;
                    }
                }
                catch
                {
                    // Skip failed responses
                }
            }

            if (validResponses > 0)
            {
                var avgDouble = Math.Round(totalScore / validResponses, 2);
                var sentimentLabel = avgDouble > 0
                    ? "Positive"
                    : avgDouble < 0
                        ? "Negative"
                        : "Neutral";

                result.AverageScore = $"{sentimentLabel}: {avgDouble.ToString("0.00")}";
            }
            else
            {
                result.AverageScore = "N/A (no valid responses)";
            }

            return result;
        }

        /// <summary>
        /// Legacy method; use GetSentimentDataAsync for Details view. Returns average score string only.
        /// </summary>
        public async Task<string> HuggingFaceMethod(string title)
        {
            var data = await GetSentimentDataAsync(title);
            return data.AverageScore;
        }

        public class SentimentResult
        {
            public List<string> Comments { get; set; } = new List<string>();
            public List<double> Scores { get; set; } = new List<double>();
            public string AverageScore { get; set; } = "N/A";
        }

        public class SentimentResponse
        {
            [JsonPropertyName("label")]
            public string Label { get; set; }

            [JsonPropertyName("score")]
            public float Score { get; set; }
        }

        public static async Task<List<string>> SearchRedditAsync(string searchQuery)
        {
            List<string> returnList = new List<string>();

            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("user-agent", "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 1.0.3705;)");

                string json = await client.GetStringAsync("https://api.pullpush.io/reddit/search/comment/?size=25&q=" + WebUtility.UrlEncode(searchQuery));
                JsonDocument doc = JsonDocument.Parse(json);

                // Check if "data" array exists
                if (doc.RootElement.TryGetProperty("data", out JsonElement dataArray))
                {
                    foreach (JsonElement comment in dataArray.EnumerateArray())
                    {
                        // Check if "body" exists
                        if (comment.TryGetProperty("body", out JsonElement bodyElement))
                        {
                            string textToAdd = bodyElement.GetString();
                            if (!string.IsNullOrEmpty(textToAdd))
                            {
                                textToAdd = TruncateToMaxLength(textToAdd, MaxInputLength);
                                returnList.Add(textToAdd);
                            }
                        }
                    }
                }
            }

            return returnList;
        }

        //code generated by chatgpt below

        // Truncates a string to a maximum length, safely, without splitting surrogate pairs/emoji.
        // This is the method your existing code is calling.
        private static string TruncateToMaxLength(string input, int maxLength)
        {
            if (string.IsNullOrEmpty(input) || maxLength <= 0)
                return string.Empty;

            if (input.Length <= maxLength)
                return input;

            // Delegate the actual truncation to a helper.
            return TruncateAtTextElementBoundary(input, maxLength);
        }

        // Helper that TruncateToMaxLength calls.
        // It truncates on a "text element" boundary (safer for emoji / combining chars).
        private static string TruncateAtTextElementBoundary(string input, int maxLength)
        {
            // If maxLength cuts through a surrogate pair, back up by one char.
            // This avoids producing invalid UTF-16.
            if (maxLength > 0 &&
                maxLength < input.Length &&
                char.IsHighSurrogate(input[maxLength - 1]) &&
                char.IsLowSurrogate(input[maxLength]))
            {
                maxLength--;
            }

            return input.Substring(0, maxLength);
        }



        // POST: Movies/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("id,title,IMDBlink,genre,releaseDate,mediaLink")] Movie movie)
        {
            if (ModelState.IsValid)
            {
                _context.Add(movie);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(movie);
        }

        // GET: Movies/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var movie = await _context.Movie.FindAsync(id);
            if (movie == null)
            {
                return NotFound();
            }
            return View(movie);
        }

        // POST: Movies/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int? id, [Bind("id,title,IMDBlink,genre,releaseDate,mediaLink")] Movie movie)
        {
            if (id != movie.id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(movie);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!MovieExists(movie.id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(movie);
        }

        // GET: Movies/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var movie = await _context.Movie
                .FirstOrDefaultAsync(m => m.id == id);
            if (movie == null)
            {
                return NotFound();
            }

            return View(movie);
        }

        // POST: Movies/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int? id)
        {
            var movie = await _context.Movie.FindAsync(id);
            if (movie != null)
            {
                _context.Movie.Remove(movie);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool MovieExists(int? id)
        {
            return _context.Movie.Any(e => e.id == id);
        }
    }
}
