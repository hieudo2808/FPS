using System.Text;

namespace FPS
{
    /// <summary>Mandatory story information has a subtitle and a permanent journal transcript.</summary>
    public static class CampaignDialogue
    {
        public const string InsertionBriefing = "Phi công: Sân bốc hàng nhà máy là bãi đáp đã được xác nhận. Tôi rời khu phong tỏa ngay sau khi thả đội; không có chuyến chở nội bộ. Nhiệm vụ hiện tại là thu hồi T9-17 tại kho lạnh.";
        public static string ObjectiveLine(CampaignObjectiveId id) => id switch
        {
            CampaignObjectiveId.FactoryGenerator => "Điều phối: Nguồn dự phòng đã ổn định. Đối chiếu vận đơn trước khi mở kho lạnh.",
            CampaignObjectiveId.FactoryShipping => "Điều phối: AL-04 dẫn tới An Lạc; địa chỉ này không có trong lệnh thu hồi ban đầu. Máy bay đã rời khu. Lấy kiện đối chứng rồi dùng tuyến dịch vụ nối hai cơ sở.",
            CampaignObjectiveId.FactoryCase => "Đội trưởng: Kiện T9-17 đã niêm phong. Giữ kiện này; phiếu giao nhận dẫn tới tầng B2 của An Lạc.",
            CampaignObjectiveId.AsylumAccess => "Đội trưởng: C12, AL-04, 02:40. Cùng chuyến hàng đã rời nhà máy.",
            CampaignObjectiveId.AsylumPower => "Điều phối: Nguồn dịch vụ đã có. Tủ hồ sơ tầng trên hoạt động; thang hàng cạnh phòng máy dưới hầm vẫn cần thẻ B2 từ nhà xác.",
            CampaignObjectiveId.AsylumPatient => "Đội trưởng: P046 khớp nhóm C12 và giờ tiếp nhận. Hồ sơ chuyển tiếp nằm ở nhà xác.",
            CampaignObjectiveId.AsylumTransfer => "Đội trưởng: Báo tử 03:05 nhưng chuyển theo dõi sống lúc 03:50. Thẻ B2 đã lấy. Quay lại thang hàng cạnh phòng máy dưới hầm, quét thẻ ở bảng bên phải cửa.",
            CampaignObjectiveId.LabPower => "Điều phối: Giữ mạch an toàn. Dữ liệu và thang hàng đã có nguồn.",
            CampaignObjectiveId.LabArchive => "Đội trưởng: E-02 nối T9-17, C12 và P046. Bản báo cáo đã xóa danh tính cùng yêu cầu dừng thí nghiệm.",
            CampaignObjectiveId.LabCase => "Đội trưởng: Kiện lưu trữ E-02 đã được lấy riêng. Chúng ta mang cả hồ sơ gốc và kiện đối chứng ra ngoài.",
            _ => ""
        };
        public static string ChapterLine(CampaignChapter chapter) => chapter switch
        {
            CampaignChapter.Factory => "Điều phối: Đây là Cold Ledger. Thu hồi lô T9-17 và chứng từ vận chuyển. Kiểm tra nguồn kho lạnh và vận đơn theo hai nhánh.",
            CampaignChapter.Asylum => "Đội trưởng: Đã tới An Lạc. Kiện nhà máy vẫn ở cùng đội. Kiểm tra sổ ca của bàn bảo vệ trước.",
            _ => "Đội trưởng: B2, dưới An Lạc. Tra dấu vết P046 tại điều hành và phòng thí nghiệm; tìm bản hồ sơ chưa bị sửa."
        };
        public static string PhaseLine(CampaignChapter chapter, CampaignPhase phase) => phase switch
        {
            CampaignPhase.Insertion => InsertionBriefing,
            CampaignPhase.Encounter => chapter switch
            {
                CampaignChapter.Factory => "Điều phối: Kiểm tra liên động tuyến dịch vụ, 60 giây. Bảo vệ khu điều khiển; cổng sẽ chờ đủ đội.",
                CampaignChapter.Asylum => "Điều phối: Thang B2 đang lên, 35 giây. Giữ chiếu chờ và cứu đồng đội trước khi vào cabin.",
                _ => "Điều phối: Truyền hồ sơ và gọi thang xuất hàng, 90 giây. Không cần hạ hết infected để rời cơ sở."
            },
            CampaignPhase.AwaitingParty => chapter == CampaignChapter.Asylum
                ? "Đội trưởng: Thang B2 đã tới. Vào cabin trong giếng thang cạnh phòng máy. Đủ đội trong cabin 5 giây mới đóng cửa và xuống khu thí nghiệm."
                : "Đội trưởng: Cửa đã sẵn sàng. Đưa mọi người còn sống vào vùng tập kết; không bỏ đồng đội đang ngã.",
            CampaignPhase.Transitioning => chapter == CampaignChapter.Asylum
                ? "Điều phối: Cabin đã khóa an toàn. Đang xuống B2; chờ cửa đích mở rồi ra khu tiếp nhận A."
                : "",
            CampaignPhase.Completed => "Điều phối: Bản sao đã được lưu bên ngoài. Bến dịch vụ an toàn. Khu vực vẫn phong tỏa; cuộc điều tra bắt đầu.",
            _ => ""
        };
        public static string Journal(CampaignState state)
        {
            var text=new StringBuilder("\n\nPHỤ ĐỀ ĐÃ GHI\n");
            text.AppendLine(InsertionBriefing);
            for(int i=0;i<=(int)state.chapter;i++)text.AppendLine(ChapterLine((CampaignChapter)i));
            foreach(CampaignObjectiveId id in System.Enum.GetValues(typeof(CampaignObjectiveId)))
                if(state.Has(id)) { string line=ObjectiveLine(id);if(line.Length>0)text.AppendLine(line); }
            if(state.evidenceTransmitted)text.AppendLine("Điều phối: Đã nhận bản sao đầy đủ ở tuyến ngoài. Giữ hai kiện và tập kết tại thang hàng.");
            return text.ToString();
        }

        public static string AccessInstructions(CampaignState state, CampaignObjectiveId objective)
        {
            var text = new StringBuilder();
            void Step(CampaignObjectiveId id, string description) => text.AppendLine((state.Has(id) ? "✓ " : "□ ") + description);
            if (objective == CampaignObjectiveId.FactoryRoute)
            {
                Step(CampaignObjectiveId.FactoryGenerator, "Khôi phục nguồn kho lạnh: tách dây chuyền → dự phòng → máy phát.");
                Step(CampaignObjectiveId.FactoryShipping, "Xác minh vận đơn tại khu điều vận.");
                Step(CampaignObjectiveId.FactoryCase, "Thu hồi kiện đối chứng trong kho lạnh.");
                Step(CampaignObjectiveId.FactoryRoute, "Kích hoạt tủ mở tuyến dịch vụ ở sân cuối nhà máy.");
                text.Append("Cổng chỉ mở sau chu kỳ liên động 60 giây. Đủ đội trong khu đệm 5 giây mới mở cổng phía viện.");
            }
            else if (objective == CampaignObjectiveId.AsylumLift)
            {
                Step(CampaignObjectiveId.AsylumAccess, "Nhận thẻ nhân viên tại phòng bảo vệ cạnh sảnh.");
                Step(CampaignObjectiveId.AsylumFuse, "Lấy hộp cầu chì trong kho tầng trệt.");
                Step(CampaignObjectiveId.AsylumPower, "Lắp cầu chì và cấp nguồn ở phòng máy dưới hầm.");
                Step(CampaignObjectiveId.AsylumPatient, "Xác định bệnh nhân ở hồ sơ tầng trên.");
                Step(CampaignObjectiveId.AsylumTransfer, "Đối chiếu chứng từ nhà xác, nhận thẻ B2.");
                Step(CampaignObjectiveId.AsylumLift, "Quét thẻ ở bảng bên phải thang hàng, cạnh phòng máy dưới hầm.");
                text.Append("Giữ phím tương tác 3 giây để gọi thang. Sau 35 giây, cửa mở; đủ đội trong cabin 5 giây mới xuống B2. Cửa khu A mở sau khi cả đội tới nơi.");
            }
            else text.Append("Hoàn tất nhiệm vụ hiện tại trong hồ sơ đội để mở tuyến tiếp theo.");
            return text.ToString();
        }
    }
}
