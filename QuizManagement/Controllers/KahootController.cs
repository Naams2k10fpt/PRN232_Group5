using BusinessObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services;
using System.Security.Claims;
using System.Threading.Tasks;

namespace QuizManagement.Controllers
{
    public class KahootController : Controller
    {
        private readonly IKahootService _kahootService;
        private readonly IDeckService _deckService;
        private readonly ISubjectService _subjectService;

        public KahootController(
            IKahootService kahootService,
            IDeckService deckService,
            ISubjectService subjectService)
        {
            _kahootService = kahootService;
            _deckService = deckService;
            _subjectService = subjectService;
        }

        // ==========================================
        // DÀNH CHO HOST (NGƯỜI TẠO PHÒNG - GIÁO VIÊN / CHỦ TRÌ)
        // ==========================================

        [Authorize]
        [HttpGet]
        public IActionResult Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var subjects = _subjectService.GetAllSubjects();
            ViewBag.Subjects = subjects;
            return View();
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateRoom(int deckId, string title)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var room = await _kahootService.CreateRoomAsync(deckId, title, userId);
            return RedirectToAction(nameof(Lobby), new { roomCode = room.RoomCode });
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Lobby(string roomCode)
        {
            var room = await _kahootService.GetRoomByCodeAsync(roomCode);
            if (room == null) return NotFound("Phòng không tồn tại.");

            ViewBag.Room = room;
            return View(room);
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Host(string roomCode)
        {
            var room = await _kahootService.GetRoomByCodeAsync(roomCode);
            if (room == null) return NotFound("Phòng không tồn tại.");

            ViewBag.Room = room;
            return View(room);
        }

        // ==========================================
        // DÀNH CHO PLAYER (NGƯỜI THAM GIA)
        // ==========================================

        [HttpGet]
        public IActionResult Join(string? roomCode = null)
        {
            ViewBag.RoomCode = roomCode?.Trim().ToUpperInvariant() ?? "";
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Play(string roomCode, string? nickname = null)
        {
            var room = await _kahootService.GetRoomByCodeAsync(roomCode);
            if (room == null) return NotFound("Phòng không tồn tại.");

            ViewBag.Room = room;
            ViewBag.Nickname = nickname ?? User.Identity?.Name ?? "Player";
            return View(room);
        }

        // ==========================================
        // REST API CHO MOBILE CLIENT
        // ==========================================

        [HttpGet("api/kahoot/room/{roomCode}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetRoomInfo(string roomCode)
        {
            var room = await _kahootService.GetRoomByCodeAsync(roomCode);
            if (room == null)
            {
                return NotFound(new { message = "Phòng không tồn tại" });
            }

            return Ok(new
            {
                roomCode = room.RoomCode,
                title = room.Title,
                status = room.Status,
                totalQuestions = room.TotalQuestions,
                deckName = room.Deck?.Name,
                createdAt = room.CreatedAt
            });
        }

        [HttpPost("api/kahoot/join")]
        [AllowAnonymous]
        public async Task<IActionResult> JoinRoomApi([FromBody] JoinRoomRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.RoomCode))
                return BadRequest(new { message = "Mã phòng không hợp lệ" });

            var room = await _kahootService.GetRoomByCodeAsync(request.RoomCode);
            if (room == null)
                return NotFound(new { message = "Phòng không tồn tại" });

            if (room.Status == "Finished")
                return BadRequest(new { message = "Phòng chơi đã kết thúc" });

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var participant = await _kahootService.JoinRoomAsync(
                request.RoomCode,
                request.Nickname,
                userId,
                null);

            return Ok(new
            {
                participantId = participant.Id,
                nickname = participant.Nickname,
                roomCode = room.RoomCode
            });
        }
    }

    public class JoinRoomRequest
    {
        public string RoomCode { get; set; } = null!;
        public string Nickname { get; set; } = null!;
    }
}
