using Valuer_Verification_System.Models.Data;
using Valuer_Verification_System.Models.Notice;

namespace Valuer_Verification_System.Services.Notice
{
    public interface INoticePdfService
    {
        /// <summary>
        /// Generates a single notice PDF for the given property row.
        /// </summary>
        /// <param name="row">The cleaned ExtractRow containing all property and owner data.</param>
        /// <param name="ctx">Shared batch context: letter date, review dates, logo path.</param>
        /// <returns>Raw PDF bytes ready to save or stream to the browser.</returns>
        byte[] GenerateNotice(ExtractRow row, NoticeContext ctx);
    }
}
