using ChatApp.Hubs;
using ChatApp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ChatApp.Controllers
{
    [Route("api/message")]
    public class MessageController : Controller
    {
        private readonly IHubContext<ChatHub> _chatHub;

        public MessageController(IHubContext<ChatHub> chatHub)
        {
            _chatHub = chatHub;
        }

        [HttpPost("SendMessage")]
        public async Task<IActionResult> SendMessage([FromBody] MessageDetails message)
        {
            message.MessageId = Guid.NewGuid().ToString();
            string directory = "ChatLogs";
            Directory.CreateDirectory(directory);

            string filePath = Path.Combine(directory, $"{message.To}.txt");
            string jsonLine = JsonSerializer.Serialize(message);
            await System.IO.File.AppendAllLinesAsync(filePath, new[] { jsonLine });

            await _chatHub.Clients.Group(message.To).SendAsync("ReceiveMessage", message);
            return Ok(new { status = "Message Sent" });
        }

        [HttpGet("GetHistory")]
        public IActionResult GetHistory([FromQuery] string groupName)
        {
            List<MessageDetails> history = new List<MessageDetails>();
            string filePath = Path.Combine("ChatLogs", $"{groupName}.txt");

            if (System.IO.File.Exists(filePath))
            {
                var lines = System.IO.File.ReadAllLines(filePath);
                foreach (var line in lines)
                {
                    try
                    {
                        var msg = JsonSerializer.Deserialize<MessageDetails>(line);
                        if (msg != null)
                            history.Add(msg);
                    }
                    catch { }
                }
            }

            return Ok(history);
        }

        [HttpGet("{groupName}")]
        public IActionResult GetMessages(string groupName)
        {
            string filePath = Path.Combine("ChatLogs", $"{groupName}.txt");

            if (!System.IO.File.Exists(filePath))
                return Ok(new List<MessageDetails>());

            var messages = System.IO.File.ReadAllLines(filePath)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => JsonSerializer.Deserialize<MessageDetails>(line))
                .ToList();

            return Ok(messages);
        }

        [HttpDelete("ClearChat")]
        public IActionResult ClearChat([FromQuery] string groupName)
        {
            string filePath = Path.Combine("ChatLogs", $"{groupName}.txt");
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.WriteAllText(filePath, "");
                return Ok(new { status = "Chat cleared" });
            }
            return NotFound("Chat file not found");
        }

        [HttpPost("EditMessage")]
        public IActionResult EditMessage([FromBody] EditMessageRequest request)
        {
            string filePath = Path.Combine("ChatLogs", $"{request.GroupName}.txt");

            if (!System.IO.File.Exists(filePath))
                return NotFound("Chat file not found");

            var messages = System.IO.File.ReadAllLines(filePath)
                .Select(line => JsonSerializer.Deserialize<MessageDetails>(line))
                .ToList();

            var message = messages.FirstOrDefault(m => m.MessageId == request.MessageId);
            if (message != null)
            {
                message.Message = request.NewMessage;
                message.MessageTime = DateTime.Now.ToString("hh:mm tt");
            }

            System.IO.File.WriteAllLines(filePath, messages.Select(m => JsonSerializer.Serialize(m)));
            return Ok(new { status = "Message Edited" });
        }

        [HttpPost("DeleteMessage")]
        public IActionResult DeleteMessage([FromBody] DeleteMessageRequest request)
        {
            string filePath = Path.Combine("ChatLogs", $"{request.GroupName}.txt");

            if (!System.IO.File.Exists(filePath))
                return NotFound("Chat file not found");

            var messages = System.IO.File.ReadAllLines(filePath)
                .Select(line => JsonSerializer.Deserialize<MessageDetails>(line))
                .Where(m => m.MessageId != request.MessageId)
                .ToList();

            System.IO.File.WriteAllLines(filePath, messages.Select(m => JsonSerializer.Serialize(m)));
            return Ok(new { status = "Message Deleted" });
        }

        [HttpPost("UploadFile")]
        public async Task<IActionResult> UploadFile([FromForm] IFormFile file, [FromForm] string groupName, [FromForm] string fromUser)
        {
            if (file == null || file.Length == 0)
                return BadRequest("File is empty");

            string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
            Directory.CreateDirectory(uploadsFolder);

            string fileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
            string filePath = Path.Combine(uploadsFolder, fileName);

            try
            {
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Failed to save file: {ex.Message}");
            }

            string fileUrl = $"/uploads/{fileName}";

            var message = new MessageDetails
            {
                MessageId = Guid.NewGuid().ToString(),
                Message = fileUrl,
                From = fromUser,
                To = groupName,
                MessageTime = DateTime.Now.ToString("hh:mm tt"),
                IsFile = true,
                FileName = file.FileName
            };

            string logPath = Path.Combine("ChatLogs", $"{groupName}.txt");
            await System.IO.File.AppendAllLinesAsync(logPath, new[] { JsonSerializer.Serialize(message) });

            await _chatHub.Clients.Group(groupName).SendAsync("ReceiveMessage", message);

            return Ok(new { status = "File uploaded", fileUrl, filePath });
        }

        [HttpDelete("DeleteFile")]
        public IActionResult DeleteFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return BadRequest("Filename is required");

            string nameOnly = Path.GetFileName(fileName); // protect from path injection
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads", nameOnly);

            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
                return Ok(new { success = true });
            }

            return NotFound(new { error = "File not found" });
        }
    }
}
