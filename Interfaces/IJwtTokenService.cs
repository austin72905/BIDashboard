using BIDashboardBackend.Models;

namespace BIDashboardBackend.Interfaces
{
    public interface IJwtTokenService
    {
        /// <summary>
        /// 產生 Access JWT
        /// </summary>
        /// <param name="user">你系統內的使用者</param>
        /// <param name="lifetime">存活時間（預設用設定檔），可在呼叫時覆蓋</param>
        /// <param name="extraClaims">額外要放進 token 的 claims（可選）</param>
        string Generate(User user, TimeSpan? lifetime = null, IDictionary<string, string>? extraClaims = null);
    }
}
