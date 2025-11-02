using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using System.IO;

namespace QCGO.Controllers
{
    [Route("img")]
    public class ImageController : Controller
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public ImageController(IHttpClientFactory httpFactory, IWebHostEnvironment webHostEnvironment)
        {
            _httpFactory = httpFactory;
            _webHostEnvironment = webHostEnvironment;
        }

        // GET /img/fetch/{base64Url}
        [HttpGet("fetch/{b64}")]
        public async Task<IActionResult> Fetch(string b64)
        {
            if (b64?.Length > 2000) return BadRequest("url too long");
            if (string.IsNullOrEmpty(b64)) return BadRequest();

            string url;
            try
            {
                var bytes = System.Convert.FromBase64String(b64);
                url = System.Text.Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                return BadRequest();
            }

            // Handle local files (paths starting with ~/ or /)
            if (url.StartsWith("~/") || url.StartsWith("/"))
            {
                try
                {
                    var webRootPath = _webHostEnvironment.WebRootPath;
                    
                    // Remove ~/ prefix if present
                    var relativePath = url.StartsWith("~/") ? url.Substring(2) : url.TrimStart('/');
                    
                    // Security: prevent directory traversal attacks
                    if (relativePath.Contains("..") || Path.IsPathRooted(relativePath))
                    {
                        return BadRequest("Invalid file path");
                    }

                    var fullPath = Path.Combine(webRootPath, relativePath);

                    // Check if file exists
                    if (!System.IO.File.Exists(fullPath))
                    {
                        return NotFound($"File not found: {relativePath}");
                    }

                    // Get content type based on file extension
                    var contentType = GetContentType(fullPath);
                    
                    // Serve the local file
                    return PhysicalFile(fullPath, contentType);
                }
                catch (Exception ex)
                {
                    return StatusCode(500, $"Error serving local file: {ex.Message}");
                }
            }

            // Handle HTTP URLs (existing logic)
            if (!url.StartsWith("http://") && !url.StartsWith("https://")) 
                return BadRequest("URL must be http, https, or a local path starting with ~/ or /");

            var client = _httpFactory.CreateClient();
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("image/*"));
            
            // Set timeout for external requests
            client.Timeout = TimeSpan.FromSeconds(30);

            try
            {
                using var resp = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                if (!resp.IsSuccessStatusCode) return StatusCode((int)resp.StatusCode);

                var contentType = resp.Content.Headers.ContentType?.MediaType ?? string.Empty;

                if (contentType.Contains("text/html"))
                {
                    var html = await resp.Content.ReadAsStringAsync();

                    string? found = null;

                    // look for og:image
                    var m = System.Text.RegularExpressions.Regex.Match(html, "<meta[^>]+property=[\'\"]og:image[\'\"][^>]+content=[\'\"]([^\'\"]+)[\'\"]", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (m.Success) found = m.Groups[1].Value;

                    // fallback: meta name
                    if (found == null)
                    {
                        m = System.Text.RegularExpressions.Regex.Match(html, "<meta[^>]+name=[\'\"]og:image[\'\"][^>]+content=[\'\"]([^\'\"]+)[\'\"]", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (m.Success) found = m.Groups[1].Value;
                    }

                    // fallback: link rel=image_src
                    if (found == null)
                    {
                        m = System.Text.RegularExpressions.Regex.Match(html, "<link[^>]+rel=[\'\"]image_src[\'\"][^>]+href=[\'\"]([^\'\"]+)[\'\"]", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (m.Success) found = m.Groups[1].Value;
                    }

                    // fallback: first <img src=>
                    if (found == null)
                    {
                        m = System.Text.RegularExpressions.Regex.Match(html, "<img[^>]+src=[\'\"]([^\'\"]+)[\'\"]", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (m.Success) found = m.Groups[1].Value;
                    }

                    if (!string.IsNullOrEmpty(found))
                    {
                        try
                        {
                            var resolved = new Uri(new Uri(url), found).AbsoluteUri;
                            using var imgResp = await client.GetAsync(resolved, HttpCompletionOption.ResponseHeadersRead);
                            if (imgResp.IsSuccessStatusCode)
                            {
                                var imgContentType = imgResp.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
                                var imgData = await imgResp.Content.ReadAsByteArrayAsync();
                                return File(imgData, imgContentType);
                            }
                        }
                        catch
                        {
                            // continue to return the original HTML response if image fetch fails
                        }
                    }

                    // if we couldn't find an image, return 415 unsupported media type
                    return StatusCode(415, "No image found at URL");
                }

                var data = await resp.Content.ReadAsByteArrayAsync();
                return File(data, resp.Content.Headers.ContentType?.ToString() ?? "application/octet-stream");
            }
            catch (Exception ex)
            {
                return StatusCode(502, ex.Message);
            }
        }

        /// <summary>
        /// Gets the MIME content type based on file extension
        /// </summary>
        private string GetContentType(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".bmp" => "image/bmp",
                ".svg" => "image/svg+xml",
                ".ico" => "image/x-icon",
                _ => "application/octet-stream"
            };
        }
    }
}