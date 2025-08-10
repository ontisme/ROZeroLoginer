using System;
using System.IO;
using System.Text.RegularExpressions;

namespace ROZeroLoginer.Services
{
    public class GameResolutionService
    {
        private int _width = 1024;
        private int _height = 768;

        public int Width => _width;
        public int Height => _height;

        public bool LoadResolutionFromConfig(string roGamePath)
        {
            try
            {
                if (string.IsNullOrEmpty(roGamePath) || !File.Exists(roGamePath))
                    return false;

                var gameDirectory = Path.GetDirectoryName(roGamePath);
                var optionInfoPath = Path.Combine(gameDirectory, "savedata", "OptionInfo.lua");

                if (!File.Exists(optionInfoPath))
                    return false;

                var content = File.ReadAllText(optionInfoPath);

                // Parse WIDTH
                var widthMatch = Regex.Match(content, @"OptionInfoList\[""WIDTH""\]\s*=\s*(\d+)");
                if (widthMatch.Success)
                {
                    _width = int.Parse(widthMatch.Groups[1].Value);
                }

                // Parse HEIGHT
                var heightMatch = Regex.Match(content, @"OptionInfoList\[""HEIGHT""\]\s*=\s*(\d+)");
                if (heightMatch.Success)
                {
                    _height = int.Parse(heightMatch.Groups[1].Value);
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading resolution: {ex.Message}");
                return false;
            }
        }

        public (int x, int y) GetAgreeButtonPosition()
        {
            // 同意按鈕位置根據實際測試數據
            // 1920x1080: (1084, 635) - 56.46%, 58.80%
            // 1600x900: (921, 583) - 57.56%, 64.78%
            // 1280x720: (763, 495) - 59.61%, 68.75%
            // 1024x768: (634, 518) - 61.91%, 67.45%

            int x, y;

            // 特定解析度的精確位置
            if (_width == 1920 && _height == 1080)
            {
                x = 1084;
                y = 635;
            }
            else if (_width == 1600 && _height == 900)
            {
                x = 921;
                y = 583;
            }
            else if (_width == 1280 && _height == 720)
            {
                x = 763;
                y = 495;
            }
            else if (_width == 1024 && _height == 768)
            {
                x = 634;
                y = 518;
            }
            else if (_width == 800 && _height == 600)
            {
                x = 524;
                y = 235;
            }
            else
            {
                // 對於其他解析度，使用線性插值
                // 觀察到的規律：解析度越小，按鈕位置的比例越大
                // 使用寬度作為基準進行插值

                double xRatio, yRatio;

                if (_width >= 1920)
                {
                    xRatio = 0.5646;
                    yRatio = 0.588;
                }
                else if (_width >= 1600)
                {
                    // 在 1600-1920 之間插值
                    double t = (_width - 1600.0) / (1920.0 - 1600.0);
                    xRatio = 0.5756 + (0.5646 - 0.5756) * t;
                    yRatio = 0.6478 + (0.588 - 0.6478) * t;
                }
                else if (_width >= 1280)
                {
                    // 在 1280-1600 之間插值
                    double t = (_width - 1280.0) / (1600.0 - 1280.0);
                    xRatio = 0.5961 + (0.5756 - 0.5961) * t;
                    yRatio = 0.6875 + (0.6478 - 0.6875) * t;
                }
                else if (_width >= 1024)
                {
                    // 在 1024-1280 之間插值
                    double t = (_width - 1024.0) / (1280.0 - 1024.0);
                    xRatio = 0.6191 + (0.5961 - 0.6191) * t;
                    yRatio = 0.6745 + (0.6875 - 0.6745) * t;
                }
                else
                {
                    // 小於 1024 的解析度
                    xRatio = 0.62;
                    yRatio = 0.675;
                }

                x = (int)(_width * xRatio);
                y = (int)(_height * yRatio);
            }

            return (x, y);
        }

        public (int x, int y) GetLoginButtonPosition()
        {
            // 登入按鈕的位置計算
            double xRatio = 0.5;
            double yRatio = 0.65;

            int x = (int)(_width * xRatio);
            int y = (int)(_height * yRatio);

            return (x, y);
        }
    }
}