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
            message.MessageTime ??= DateTime.Now.ToString("hh:mm tt");
            message.IsForwarded ??= "false";
            message.ForwardedTo ??= "";
            message.IsReplied ??= "false";
            message.RepliedTo ??= "";

            var directory = Path.Combine("ChatLogs");
            Directory.CreateDirectory(directory);

            var filePath = Path.Combine(directory, $"{message.To}.txt");
            var json = JsonSerializer.Serialize(message);
            await System.IO.File.AppendAllLinesAsync(filePath, new[] { json });

            await _chatHub.Clients.Group(message.To).SendAsync("ReceiveMessage", message);

            //await _chatHub.Clients.Group(message.To).SendAsync("ReceiveMessage", message);
            return Ok(new { status = "Message Sent" });
        }

        [HttpPost("UploadFile")]
        public async Task<IActionResult> UploadFile([FromForm] IFormFile file, [FromForm] string groupName, [FromForm] string fromUser)
        {
            if (file == null || file.Length == 0)
                return BadRequest("File is empty");

            var uploadsFolder = Path.Combine("wwwroot", "uploads");
            Directory.CreateDirectory(uploadsFolder);

            var fileName = Guid.NewGuid() + "_" + Path.GetFileName(file.FileName);
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var message = new MessageDetails
            {
                MessageId = Guid.NewGuid().ToString(),
                Message = "/uploads/" + fileName,
                From = fromUser,
                To = groupName,
                MessageTime = DateTime.Now.ToString("hh:mm tt"),
                IsFile = true,
                FileName = file.FileName
            };

            var logPath = Path.Combine("ChatLogs", $"{groupName}.txt");
            await System.IO.File.AppendAllLinesAsync(logPath, new[] { JsonSerializer.Serialize(message) });

            await _chatHub.Clients.Group(groupName).SendAsync("ReceiveMessage", message);
            return Ok(new { status = "File uploaded", message });
        }

        [HttpGet("GetHistory")]
        public IActionResult GetHistory([FromQuery] string groupName)
        {
            var history = new List<MessageDetails>();
            var filePath = Path.Combine("ChatLogs", $"{groupName}.txt");

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

        [HttpPost("EditMessage")]
        public IActionResult EditMessage([FromBody] EditMessageRequest request)
        {
            var filePath = Path.Combine("ChatLogs", $"{request.GroupName}.txt");
            if (!System.IO.File.Exists(filePath)) return NotFound();

            var messages = System.IO.File.ReadAllLines(filePath)
                .Select(line => JsonSerializer.Deserialize<MessageDetails>(line))
                .ToList();

            var target = messages.FirstOrDefault(m => m.MessageId == request.MessageId);
            if (target != null)
            {
                target.Message = request.NewMessage;
                target.IsEdited = true;
                target.MessageTime = DateTime.Now.ToString("hh:mm tt");
            }

            System.IO.File.WriteAllLines(filePath, messages.Select(m => JsonSerializer.Serialize(m)));
            return Ok(new { status = "Edited" });
        }

        [HttpPost("DeleteMessage")]
        public IActionResult DeleteMessage([FromBody] DeleteMessageRequest request)
        {
            var filePath = Path.Combine("ChatLogs", $"{request.GroupName}.txt");
            if (!System.IO.File.Exists(filePath)) return NotFound();

            var messages = System.IO.File.ReadAllLines(filePath)
                .Select(line => JsonSerializer.Deserialize<MessageDetails>(line))
                .Where(m => m.MessageId != request.MessageId)
                .ToList();

            System.IO.File.WriteAllLines(filePath, messages.Select(m => JsonSerializer.Serialize(m)));
            return Ok(new { status = "Deleted" });
        }

        [HttpDelete("ClearChat")]
        public IActionResult ClearChat([FromQuery] string groupName)
        {
            var filePath = Path.Combine("ChatLogs", $"{groupName}.txt");
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.WriteAllText(filePath, "");
                return Ok(new { status = "Cleared" });
            }
            return NotFound();
        }
    }
}
