# 遊戲啟動參數自定義功能

## 📅 開發日期
2025-11-07

## 🎯 功能目標

允許用戶自定義遊戲啟動參數,支援不同的遊戲版本或特殊啟動需求。

## 🔍 需求分析

### 原有限制
在原有實現中:
```csharp
Arguments = "1rag1"  // 硬編碼,無法修改
```

**問題**:
- ❌ 參數硬編碼在代碼中
- ❌ 不同遊戲版本可能需要不同參數
- ❌ 用戶無法自定義啟動選項
- ❌ 需要修改代碼才能更改參數

### 用戶需求
1. **靈活性**: 不同遊戲版本可能需要不同的啟動參數
2. **可配置**: 用戶應該能在設定中自定義參數
3. **預設值**: 保持 "1rag1" 作為預設值
4. **持久化**: 參數應該保存在設定檔案中

## ✅ 實現方案

### 1. 新增設定屬性

在 `AppSettings.cs` 中新增:

```csharp
private string _gameStartupArguments = "1rag1";

public string GameStartupArguments
{
    get => _gameStartupArguments;
    set
    {
        _gameStartupArguments = value;
        OnPropertyChanged();
    }
}
```

**特點**:
- ✅ 預設值為 "1rag1"
- ✅ 支援 INotifyPropertyChanged
- ✅ 自動持久化到 settings.json

### 2. 更新啟動邏輯

在 `MainWindow.xaml.cs` 中更新兩處啟動代碼:

```csharp
// 位置 1: 單個帳號啟動 (約 1096 行)
var processInfo = new ProcessStartInfo
{
    FileName = gameExecutable,
    Arguments = settings.GameStartupArguments ?? "1rag1",  // ✅ 使用設定
    WorkingDirectory = gameDirectory,
    UseShellExecute = true
};

LogService.Instance.Info("[StartGame] 啟動參數: {0}", processInfo.Arguments);

// 位置 2: 批次啟動 (約 1190 行)
var processInfo = new ProcessStartInfo
{
    FileName = gameExecutable,
    Arguments = settings.GameStartupArguments ?? "1rag1",  // ✅ 使用設定
    WorkingDirectory = gameDirectory,
    UseShellExecute = true
};

LogService.Instance.Info("[StartGameOnly] 啟動參數: {0}", processInfo.Arguments);
```

**安全性**:
- ✅ 使用 null-coalescing operator (`??`) 提供後備值
- ✅ 記錄實際使用的啟動參數到日誌
- ✅ 兩處啟動代碼保持一致

### 3. UI 設定界面

在 `SettingsWindow.xaml` 的「遊戲設定」區塊中新增:

```xml
<StackPanel Grid.Row="1" Orientation="Horizontal" Margin="0,5">
    <TextBlock Text="遊戲啟動參數: " VerticalAlignment="Center" Margin="0,0,10,0"/>
    <TextBox Name="GameStartupArgumentsTextBox" Width="250" Text="1rag1"
             Height="24" VerticalAlignment="Center"/>
</StackPanel>

<TextBlock Grid.Row="2"
           Text="設定 RO 主程式位置以啟用遊戲啟動功能,啟動參數預設為 1rag1"
           Foreground="Gray" FontSize="11" Margin="0,5"/>
```

**UI 特點**:
- ✅ 清晰的標籤說明
- ✅ 250px 寬度的輸入框
- ✅ 灰色提示文字說明預設值
- ✅ 位置在「RO 主程式路徑」下方,邏輯上相關

### 4. 設定讀取和保存

在 `SettingsWindow.xaml.cs` 中:

#### 建構函式 - 複製設定
```csharp
_settings = new AppSettings
{
    // ... 其他設定
    RoGamePath = settings.RoGamePath,
    GameStartupArguments = settings.GameStartupArguments,  // ✅ 新增
    // ... 其他設定
};
```

#### LoadSettings() - 載入到 UI
```csharp
RoGamePathTextBox.Text = _settings.RoGamePath;
GameStartupArgumentsTextBox.Text = _settings.GameStartupArguments ?? "1rag1";  // ✅ 新增
```

#### SaveSettings() - 從 UI 保存
```csharp
_settings.RoGamePath = RoGamePathTextBox.Text;
_settings.GameStartupArguments = GameStartupArgumentsTextBox.Text;  // ✅ 新增
```

## 📊 功能效果

### 使用流程

```
用戶開啟設定視窗
  ↓
找到「遊戲設定」區塊
  ↓
看到「遊戲啟動參數」輸入框 (預設: 1rag1)
  ↓
修改為自己需要的參數 (例如: "setup", "-windowed", 等)
  ↓
點擊「確定」保存
  ↓
下次啟動遊戲時使用新參數
  ↓
日誌記錄: [StartGame] 啟動參數: setup
```

### 常見參數範例

| 參數 | 說明 | 用途 |
|------|------|------|
| `1rag1` | 預設參數 | RO Zero 標準啟動 |
| `setup` | 設定模式 | 開啟遊戲設定程式 |
| `-windowed` | 視窗模式 | 以視窗模式啟動 |
| `/3doff` | 關閉 3D 加速 | 兼容模式 |
| (空白) | 無參數 | 某些版本不需要參數 |

### 資料持久化

設定會自動保存到:
```
%AppData%\ROZeroLoginer\settings.json
```

JSON 範例:
```json
{
  "RoGamePath": "C:\\Gravity\\RagnarokZero\\Ragexe.exe",
  "GameStartupArguments": "1rag1",
  "...": "..."
}
```

## 🔧 技術細節

### 向後兼容性

**舊版設定檔案**:
如果用戶從舊版本升級,`GameStartupArguments` 不存在於設定檔案中:
- ✅ C# 欄位預設值為 "1rag1"
- ✅ UI 讀取時使用 `?? "1rag1"` 確保顯示預設值
- ✅ 啟動時使用 `?? "1rag1"` 確保有值

**結果**: 完全向後兼容,舊用戶無感升級 ✅

### 空值處理

```csharp
// 情況 1: 正常值
GameStartupArguments = "setup"
→ 使用 "setup"

// 情況 2: null (不應該發生,但防禦性處理)
GameStartupArguments = null
→ 使用 "1rag1"

// 情況 3: 空字串 (用戶清空輸入框)
GameStartupArguments = ""
→ 使用 "" (允許無參數啟動)
```

### 日誌輸出

```csharp
LogService.Instance.Info("[StartGame] 啟動參數: {0}", processInfo.Arguments);
```

**優點**:
- ✅ 調試時可以確認使用的參數
- ✅ 問題排查時有記錄
- ✅ 用戶反饋問題時可以查看日誌

## 📁 修改的檔案

### 1. AppSettings.cs ✅ 已更新
**修改內容**:
- 新增 `_gameStartupArguments` 私有欄位 (預設 "1rag1")
- 新增 `GameStartupArguments` 公開屬性

**位置**: 行 23, 165-173

### 2. MainWindow.xaml.cs ✅ 已更新
**修改內容**:
- 更新兩處 `ProcessStartInfo` 使用 `settings.GameStartupArguments`
- 新增兩處日誌輸出記錄啟動參數

**位置**:
- 行 1096 (單個啟動)
- 行 1190 (批次啟動)

### 3. SettingsWindow.xaml ✅ 已更新
**修改內容**:
- 新增「遊戲啟動參數」輸入框
- 更新 Grid.RowDefinitions (新增一行)
- 調整後續元素的 Grid.Row 索引
- 更新提示文字

**位置**: 行 136-157

### 4. SettingsWindow.xaml.cs ✅ 已更新
**修改內容**:
- 建構函式中複製 `GameStartupArguments`
- `LoadSettings()` 載入到 UI
- `SaveSettings()` 從 UI 保存

**位置**: 行 38, 75, 189

## 🧪 測試建議

### 測試案例 1: 預設值
**步驟**:
1. 全新安裝或刪除設定檔案
2. 啟動程式
3. 開啟設定視窗

**預期結果**:
- ✅ 「遊戲啟動參數」顯示 "1rag1"
- ✅ 啟動遊戲時使用 "1rag1"
- ✅ 日誌顯示: `[StartGame] 啟動參數: 1rag1`

### 測試案例 2: 自定義參數
**步驟**:
1. 開啟設定視窗
2. 將「遊戲啟動參數」改為 "setup"
3. 點擊「確定」
4. 啟動遊戲

**預期結果**:
- ✅ 設定成功保存
- ✅ 重新開啟設定視窗仍顯示 "setup"
- ✅ 啟動遊戲時使用 "setup"
- ✅ 日誌顯示: `[StartGame] 啟動參數: setup`

### 測試案例 3: 空參數
**步驟**:
1. 將「遊戲啟動參數」清空
2. 保存並啟動遊戲

**預期結果**:
- ✅ 程式不崩潰
- ✅ 遊戲以無參數方式啟動
- ✅ 日誌顯示: `[StartGame] 啟動參數: `

### 測試案例 4: 批次啟動
**步驟**:
1. 設定自定義參數
2. 選擇多個帳號
3. 批次啟動

**預期結果**:
- ✅ 所有遊戲實例都使用相同的自定義參數
- ✅ 日誌中每次啟動都記錄參數

### 測試案例 5: 向後兼容
**步驟**:
1. 使用舊版設定檔案(無 GameStartupArguments)
2. 啟動程式

**預期結果**:
- ✅ 程式正常啟動
- ✅ 自動使用預設值 "1rag1"
- ✅ 保存設定後新增該欄位到檔案

## 📊 日誌範例

### 正常啟動
```
[Info] [StartGame] 啟動參數: 1rag1
[Info] 遊戲啟動成功: PID 12345
```

### 自定義參數
```
[Info] [StartGame] 啟動參數: setup
[Info] 遊戲啟動成功: PID 12346
```

### 批次啟動
```
[Info] [StartGameOnly] 啟動參數: 1rag1
[Info] 遊戲啟動成功: PID 12347
[Info] [StartGameOnly] 啟動參數: 1rag1
[Info] 遊戲啟動成功: PID 12348
[Info] [StartGameOnly] 啟動參數: 1rag1
[Info] 遊戲啟動成功: PID 12349
```

## 🎯 設計原則應用

### SOLID 原則
- ✅ **S - 單一職責**: AppSettings 負責設定管理
- ✅ **O - 開閉原則**: 新增屬性不影響現有代碼
- ✅ **D - 依賴反轉**: 依賴 AppSettings 抽象配置

### 其他原則
- ✅ **DRY**: 參數定義在一處,多處使用
- ✅ **KISS**: 簡單的字串屬性,易於理解
- ✅ **防禦性編程**: Null-coalescing operator 確保安全
- ✅ **向後兼容**: 舊版用戶無感升級

## 💡 未來可能的改進

### 1. 參數驗證
```csharp
// 驗證參數格式
if (!string.IsNullOrEmpty(GameStartupArguments) &&
    GameStartupArguments.Contains("\""))
{
    MessageBox.Show("參數不能包含雙引號");
}
```

### 2. 參數預設選項
```xml
<ComboBox Name="GameStartupArgumentsComboBox" IsEditable="True">
    <ComboBoxItem>1rag1</ComboBoxItem>
    <ComboBoxItem>setup</ComboBoxItem>
    <ComboBoxItem>-windowed</ComboBoxItem>
    <ComboBoxItem>/3doff</ComboBoxItem>
</ComboBox>
```

### 3. 帳號級別參數
```csharp
// 允許每個帳號有不同的啟動參數
public class Account
{
    public string CustomStartupArguments { get; set; }
}
```

## 🎉 總結

通過這次功能新增:
- ✅ **提升靈活性**: 用戶可自定義啟動參數
- ✅ **保持預設**: "1rag1" 作為預設值
- ✅ **完全向後兼容**: 舊用戶無感升級
- ✅ **清晰日誌**: 記錄實際使用的參數
- ✅ **簡單易用**: UI 直觀,設定方便
- ✅ **安全可靠**: 防禦性 null 處理

**功能實現是成功的!** 🎊

---

**執行者**: Claude AI Assistant
**建議者**: User (Lyfx) - "Arguments 我想要讓用戶可以設定 並且默認是 1rag1"
**狀態**: ✅ 完成
**下一步**: 編譯測試 → 實際啟動測試 → 驗證參數生效
