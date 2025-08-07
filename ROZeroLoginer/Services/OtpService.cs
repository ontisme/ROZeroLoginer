using System;
using System.Threading.Tasks;
using OtpNet;

namespace ROZeroLoginer.Services
{
    /// <summary>
    /// OTP 服務類，使用 OtpNet 庫實現 TOTP 功能
    /// </summary>
    public class OtpService
    {
        /// <summary>
        /// 生成 TOTP 代碼
        /// </summary>
        /// <param name="secret">Base32 編碼的密鑰</param>
        /// <param name="digits">OTP 位數，預設為 6</param>
        /// <param name="period">時間週期（秒），預設為 30</param>
        /// <returns>TOTP 代碼</returns>
        public string GenerateTotp(string secret, int digits = 6, int period = 30)
        {
            try
            {
                var cleanedSecret = CleanSecret(secret);
                var secretBytes = Base32Encoding.ToBytes(cleanedSecret);
                var totp = new Totp(secretBytes, step: period, totpSize: digits);
                
                return totp.ComputeTotp(DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                throw new Exception($"TOTP generation error: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 獲取當前時間週期的剩餘時間（秒）
        /// </summary>
        /// <param name="period">時間週期（秒），預設為 30</param>
        /// <returns>剩餘時間（秒）</returns>
        public int GetTimeRemaining(int period = 30)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var timeInPeriod = (int)(now % period);
            return period - timeInPeriod;
        }

        /// <summary>
        /// 異步生成 TOTP，支援時間控制
        /// </summary>
        /// <param name="secret">Base32 編碼的密鑰</param>
        /// <param name="digits">OTP 位數，預設為 6</param>
        /// <param name="period">時間週期（秒），預設為 30</param>
        /// <param name="minimumTimeRemaining">最小剩餘時間（秒），預設為 2</param>
        /// <returns>TOTP 代碼</returns>
        public async Task<string> GenerateTotpWithTimingAsync(string secret, int digits = 6, int period = 30, int minimumTimeRemaining = 2)
        {
            var timeRemaining = GetTimeRemaining(period);
            
            if (timeRemaining <= minimumTimeRemaining)
            {
                // 等待到下一個週期開始
                var waitTime = timeRemaining * 1000; // 轉換為毫秒
                await Task.Delay(waitTime + 100); // 額外等待100ms確保新週期開始
            }
            
            return GenerateTotp(secret, digits, period);
        }

        /// <summary>
        /// 同步生成 TOTP，支援時間控制
        /// </summary>
        /// <param name="secret">Base32 編碼的密鑰</param>
        /// <param name="digits">OTP 位數，預設為 6</param>
        /// <param name="period">時間週期（秒），預設為 30</param>
        /// <param name="minimumTimeRemaining">最小剩餘時間（秒），預設為 2</param>
        /// <returns>TOTP 代碼</returns>
        public string GenerateTotpWithTiming(string secret, int digits = 6, int period = 30, int minimumTimeRemaining = 2)
        {
            var timeRemaining = GetTimeRemaining(period);
            
            if (timeRemaining <= minimumTimeRemaining)
            {
                // 等待到下一個週期開始
                var waitTime = timeRemaining * 1000; // 轉換為毫秒
                System.Threading.Thread.Sleep(waitTime + 100); // 額外等待100ms確保新週期開始
            }
            
            return GenerateTotp(secret, digits, period);
        }

        /// <summary>
        /// 驗證 TOTP 代碼
        /// </summary>
        /// <param name="secret">Base32 編碼的密鑰</param>
        /// <param name="inputCode">用戶輸入的代碼</param>
        /// <param name="tolerance">容錯範圍，預設為 1</param>
        /// <param name="digits">OTP 位數，預設為 6</param>
        /// <param name="period">時間週期（秒），預設為 30</param>
        /// <returns>驗證結果</returns>
        public bool VerifyTotp(string secret, string inputCode, int tolerance = 1, int digits = 6, int period = 30)
        {
            try
            {
                var cleanedSecret = CleanSecret(secret);
                var secretBytes = Base32Encoding.ToBytes(cleanedSecret);
                var totp = new Totp(secretBytes, step: period, totpSize: digits);
                
                // OtpNet 的 VerifyTotp 方法內建容錯功能
                var window = new VerificationWindow(previous: tolerance, future: tolerance);
                return totp.VerifyTotp(inputCode, out long timeStepMatched, window);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 清理密鑰字串，移除無效字符
        /// </summary>
        /// <param name="secret">原始密鑰</param>
        /// <returns>清理後的密鑰</returns>
        private string CleanSecret(string secret)
        {
            if (string.IsNullOrEmpty(secret))
                return string.Empty;

            // 移除空白字符和常見分隔符
            var cleaned = secret.Replace(" ", "").Replace("-", "").Replace("_", "").ToUpperInvariant();
            
            // Base32 字符集驗證和清理
            const string base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
            var result = string.Empty;
            
            foreach (char c in cleaned)
            {
                if (base32Alphabet.Contains(c.ToString()))
                {
                    result += c;
                }
            }
            
            return result;
        }
    }
}