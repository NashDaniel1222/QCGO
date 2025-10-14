using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc;

namespace QCGO.Controllers
{
    [Route("img")]
    public class ImageController : Controller
    {
        private readonly IHttpClientFactory _httpFactory;

        public ImageController(IHttpClientFactory httpFactory)
        {
            _httpFactory = httpFactory;
        }

        // GET /img/fetch/{base64Url}
        [HttpGet("fetch/{b64}")]
        public async Task<IActionResult> Fetch(string b64)
        {
            // guard: limit base64 length to reasonable size (~2k chars -> ~1.5KB URL)
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

            if (!url.StartsWith("http://") && !url.StartsWith("https://")) return BadRequest();

            var client = _httpFactory.CreateClient();
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("image/*"));

            try
            {
                using var resp = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                if (!resp.IsSuccessStatusCode) return StatusCode((int)resp.StatusCode);

                var contentType = resp.Content.Headers.ContentType?.MediaType ?? string.Empty;

                // If the response is HTML, try to extract an image URL from meta tags or first <img>
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
    }
}
