# ROZero Loginer

🎮 **專為 TWRO 系列設計的帳號管理工具** - 支援台版仙境傳說正服與樂園版本

A Ragnarok Online account management tool specifically designed for TWRO series (Official & Paradise servers) with OTP authentication and auto-login functionality.

![Screenshot](https://i.ibb.co/v4PkJ7T5/image.png)

## ✨ 主要功能

- 🔐 **加密帳號儲存** - AES 加密保護敏感資料
- 🚀 **一鍵自動登入** - 支援帳號、密碼、TOTP 自動輸入
- ⌨️ **全域熱鍵** - 預設 HOME 鍵快速呼叫
- 🎯 **遊戲視窗偵測** - 僅在 RO 遊戲視窗啟動時作用
- 🔑 **OTP 雙重驗證** - 即時產生驗證碼

## 📋 系統需求

- Windows 10/11
- .NET Framework 4.8
- 台版仙境傳說（正服/樂園，或是流程相同的伺服器皆可）

## 🚀 安裝使用

1. 前往 [Releases](https://github.com/ontisme/ROZeroLoginer/releases) 下載最新版本
2. 執行 `ROZeroLoginer.exe`（免安裝）
3. 新增帳號資訊（帳號、密碼、TOTP 密鑰）
4. 在遊戲登入畫面按 `HOME` 鍵選擇帳號

## 🔒 安全性

- 所有資料本地加密儲存，不傳輸至外部
- 僅在檢測到 RO 遊戲視窗時啟動

## ⚠️ 免責聲明

本工具僅供個人便利使用，使用者需自行承擔風險。作者不對任何可能產生的問題負責。

## 🤝 Contributing | 貢獻

Contributions are welcome! Please feel free to submit a Pull Request.

歡迎貢獻！請隨時提交 Pull Request。

### Development Setup | 開發環境設置
```bash
git clone https://github.com/ontisme/ROZeroLoginer.git
cd ROZeroLoginer
# Open ROZeroLoginer.sln in Visual Studio
```

## 📝 License | 授權

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

本專案採用 MIT 授權 - 詳見 [LICENSE](LICENSE) 檔案。

---

**Made with ❤️ by [ontisme](https://github.com/ontisme)**

如有問題或建議，歡迎在 [Issues](https://github.com/ontisme/ROZeroLoginer/issues) 頁面回報。

For questions or suggestions, please report them on the [Issues](https://github.com/ontisme/ROZeroLoginer/issues) page.
