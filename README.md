# ROZero Loginer

🎮 **Ragnarok Online Zero Account Management Tool** | **仙境傳說 Zero 帳號管理工具**

A modern WPF application for managing Ragnarok Online Zero accounts with TOTP authentication and auto-login functionality.

一個現代化的 WPF 應用程式，用於管理仙境傳說 Zero 帳號，支援 TOTP 驗證和自動登入功能。

## ✨ Features | 功能特色

### 🔐 Security | 安全性
- **Encrypted Account Storage** | **加密帳號儲存** - AES encryption for sensitive data
- **TOTP Authentication** | **TOTP 雙重驗證** - Time-based One-Time Password support
- **Secure Password Management** | **安全密碼管理** - Protected credential storage

### 🚀 Automation | 自動化
- **Global Hotkey Support** | **全域熱鍵支援** - Quick account selection (default: HOME key)
- **Auto-Login Functionality** | **自動登入功能** - Automated input of username, password, and OTP
- **Game Window Detection** | **遊戲視窗偵測** - Only activates when RO game window is active

### 🎨 User Interface | 使用者介面
- **Modern shadcn/ui Design** | **現代化 shadcn/ui 設計** - Clean and intuitive interface
- **Real-time TOTP Display** | **即時 TOTP 顯示** - Live countdown and code generation
- **Account Management** | **帳號管理** - CRUD operations for account data

## 🖼️ Screenshots | 螢幕截圖

*Coming Soon - Add screenshots of your application here*

## 📋 Requirements | 系統需求

- **OS | 作業系統**: Windows 10/11
- **.NET Framework**: 4.8
- **Game | 遊戲**: Ragnarok Online Zero (with TOTP enabled)

## 🚀 Installation | 安裝

### Option 1: Download Release | 選項 1：下載發布版
1. Go to [Releases](https://github.com/ontisme/ROZeroLoginer/releases) page
2. Download the latest `ROZeroLoginer.exe`
3. Run the executable directly (no installation required)

前往 [Releases](https://github.com/ontisme/ROZeroLoginer/releases) 頁面
下載最新的 `ROZeroLoginer.exe`
直接執行檔案（無需安裝）

### Option 2: Build from Source | 選項 2：從原始碼建置
```bash
git clone https://github.com/ontisme/ROZeroLoginer.git
cd ROZeroLoginer/ROZeroLoginer
dotnet restore
dotnet build --configuration Release
```

## 🔧 Usage | 使用方式

### Setup | 設置
1. **Add Account | 新增帳號**
   - Click "新增帳號" (Add Account)
   - Enter account name, username, password, and TOTP secret key
   - 點擊「新增帳號」
   - 輸入帳號名稱、使用者名稱、密碼和 TOTP 密鑰

2. **Configure Hotkey | 設定熱鍵**
   - Click "設定" (Settings) to customize hotkey
   - Default hotkey is HOME key
   - 點擊「設定」自訂熱鍵
   - 預設熱鍵為 HOME 鍵

### Quick Login | 快速登入
1. Open Ragnarok Online Zero game
2. Navigate to login screen
3. Press configured hotkey (default: HOME)
4. Select desired account from popup
5. Press ENTER to auto-login

開啟仙境傳說 Zero 遊戲
導航至登入畫面
按下設定的熱鍵（預設：HOME）
從彈出視窗選擇所需帳號
按 ENTER 自動登入

## ⌨️ Keyboard Shortcuts | 鍵盤快捷鍵

| Key | Action | 功能 |
|-----|--------|------|
| `HOME` | Open account selection | 開啟帳號選擇 |
| `ENTER` | Select account | 選擇帳號 |
| `ESC` | Cancel selection | 取消選擇 |

## 🔒 Security Notes | 安全注意事項

- **Data Encryption | 資料加密**: All sensitive data is encrypted using AES
- **Local Storage | 本地儲存**: No data is transmitted to external servers
- **TOTP Security | TOTP 安全**: Time-based codes provide additional security layer

所有敏感資料皆使用 AES 加密
不會傳輸資料至外部伺服器
時間驗證碼提供額外安全層級

## 🛠️ Technical Details | 技術細節

- **Framework**: .NET Framework 4.8
- **UI Library**: WPF with shadcn/ui inspired design
- **Encryption**: AES-256
- **TOTP Algorithm**: RFC 6238 compliant
- **Build Tool**: MSBuild with Costura.Fody (single executable)

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

## ⚠️ Disclaimer | 免責聲明

This tool is for educational and convenience purposes only. Use at your own risk. The author is not responsible for any issues that may arise from using this software.

本工具僅供教育和便利目的使用。使用風險自負。作者不對使用本軟體可能產生的任何問題負責。

## 🙏 Acknowledgments | 致謝

- Inspired by modern UI/UX design principles
- Built with love for the Ragnarok Online Zero community

靈感來自現代 UI/UX 設計原則
為仙境傳說 Zero 社群用愛打造

---

**Made with ❤️ by [ontisme](https://github.com/ontisme)**

如有問題或建議，歡迎在 [Issues](https://github.com/ontisme/ROZeroLoginer/issues) 頁面回報。

For questions or suggestions, please report them on the [Issues](https://github.com/ontisme/ROZeroLoginer/issues) page.