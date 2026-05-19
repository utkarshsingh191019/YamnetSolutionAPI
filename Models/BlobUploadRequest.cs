using Microsoft.AspNetCore.Mvc;

namespace YamnetSolutionAPI.Models
{
    public class BlobUploadRequest
    {
        [FromForm(Name = "file")]
        public IFormFile file { get; set; }
        
    }
}