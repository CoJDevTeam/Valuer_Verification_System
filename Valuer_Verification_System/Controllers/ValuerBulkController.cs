using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Valuer_Verification_System.Models.ViewModels;
using Valuer_Verification_System.Services.Verifications;


namespace Valuer_Verification_System.Controllers
{
    [Authorize]
    [Route("Valuer/Bulk")]
    public sealed class ValuerBulkController : Controller
    {
        private readonly IValuerBulkVerificationService _bulk;

        public ValuerBulkController(IValuerBulkVerificationService bulk)
        {
            _bulk = bulk;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index(int? batchId, CancellationToken ct)
        {
            var username = GetUsername();

            var vm = await _bulk.GetInboxAsync(
                batchId,
                username,
                ct);

            return View(vm);
        }
        [HttpGet("Details")]
        public async Task<IActionResult> Details(
            int batchId,
            string unitKey,
            CancellationToken ct)
        {
            var username = GetUsername();

            var vm = await _bulk.GetDetailsAsync(
                batchId,
                unitKey,
                username,
                ct);

            if (vm is null)
                return NotFound();

            return View(vm);
        }

        [HttpPost("VerifyAll")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyAll(
            BulkVerifyRequestVm vm,
            CancellationToken ct)
        {
            var username = GetUsername();

            try
            {
                var count = await _bulk.VerifyAllCorrectAsync(
                    vm,
                    username,
                    ct);

                TempData["BulkSuccess"] =
                    $"Bulk verification completed. {count:N0} rows marked as correct.";
            }
            catch (Exception ex)
            {
                TempData["BulkError"] = ex.Message;
            }

            return RedirectToAction(nameof(Details), new
            {
                batchId = vm.BatchId,
                premiseId = vm.PremiseId,
                unitKey = vm.UnitKey
            });
        }

        private string GetUsername()
        {
            // Keep the full domain-qualified username (e.g. "JOBURG\10112533")
            // because ExtractRows.Username is stored in that format by the cleaning SP.
            // Stripping the domain caused every bulk query to return zero rows.
            return (
                User.FindFirst("Username")?.Value ??
                User.Identity?.Name ??
                ""
            ).Trim();
        }
    }
}