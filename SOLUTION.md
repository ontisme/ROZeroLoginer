# ROZeroLoginer 批次啟動問題完整解決方案

## 問題總結

用戶報告了以下問題:
1. **視窗無響應**: 在批次啟動多個帳號時,設定都正確但頁面不動,只有聲音,滑鼠和角色框都沒動
2. **等待時間變長**: v1.1.32和v1.4版本登入等待時間明顯變長,修改延遲設定無效
3. **未勾選自動選擇仍會進入遊戲**: 即使沒勾選"自動選擇伺服器"和"自動選擇角色",仍會自動進入遊戲,導致危險地圖角色死亡

## 根本原因分析

### 1. 視窗就緒狀態檢測不足

**位置**: `InputService.cs:1129` - `CheckRagnarokWindowFocus()`

**問題**:
```csharp
private void CheckRagnarokWindowFocus(int targetProcessId = 0)
{
    if (!IsCurrentWindowRagnarok(targetProcessId))
    {
        throw new InvalidOperationException("視窗不是RO視窗");
    }
}
```

此方法只檢查視窗是否為前台,但沒有檢查:
- 視窗是否完全載入完成
- 視窗是否真正可以接收輸入
- 視窗的繪製狀態

當批次啟動多個遊戲時,視窗可能在前台但尚未就緒,導致輸入被忽略。

### 2. 延遲設定不全面

**問題位置**:
- `AppSettings.cs:28` 定義了 `GeneralOperationDelayMs`,預設500ms
- 但這個延遲只在3個地方使用:
  - `InputService.cs:433` - PrepareWindow後
  - `InputService.cs:531` - SelectServer前
  - `InputService.cs:571` - SelectCharacter前

**缺失的延遲點**:
- 視窗焦點檢查前
- 每個輸入步驟之間
- 同意按鈕點擊後
- 帳號密碼輸入後
- OTP輸入後

### 3. 未勾選自動選擇邏輯錯誤

**位置**: `InputService.cs:527-623`

**問題代碼**:
```csharp
private void SelectServer(int server, bool autoSelectServer, int targetProcessId)
{
    Thread.Sleep(_settings.GeneralOperationDelayMs);
    CheckRagnarokWindowFocus(targetProcessId);

    if (autoSelectServer && server > 0)
    {
        // 執行選擇邏輯
    }

    // ❌ 問題: 無論是否自動選擇,都會執行Enter
    CheckRagnarokWindowFocus(targetProcessId);
    SendKey(Keys.Enter);  // 這會導致進入遊戲
}
```

**預期行為**: 如果未勾選自動選擇,應該停在選擇畫面,不執行Enter

### 4. 批次啟動流程不嚴謹

**位置**: `MainWindow.xaml.cs:1172-1247`

**問題流程**:
```csharp
private void LaunchGameForAccountInternal(Account account)
{
    // 1. 啟動遊戲進程
    var gameProcess = Process.Start(processInfo);

    // 2. 等待視窗出現
    var gameWindow = InputService.WaitForRoWindowByPid(gameProcess.Id, ...);

    // 3. 立即執行登入 ❌ 缺少就緒檢查
    Dispatcher.Invoke(() => {
        inputService.SendLogin(...);
    });
}
```

**缺失步驟**:
- 沒有確保視窗獲得焦點
- 沒有檢查視窗就緒狀態
- 沒有等待視窗完全可操作

## 解決方案設計

### 方案1: 增強視窗就緒狀態檢測

新增 `WindowReadinessService.cs`,提供:
1. **視窗繪製狀態檢測**: 檢查視窗是否正在繪製
2. **視窗響應性檢測**: 透過SendMessage檢查視窗是否響應
3. **視窗客戶區檢測**: 確認視窗有有效的客戶區
4. **綜合就緒檢查**: 結合以上所有檢測

```csharp
public class WindowReadinessService
{
    // 檢查視窗是否完全就緒並可接收輸入
    public bool IsWindowReady(IntPtr windowHandle, int maxRetries = 10, int retryDelayMs = 300);

    // 等待視窗就緒
    public bool WaitForWindowReady(IntPtr windowHandle, int timeoutMs = 5000);
}
```

### 方案2: 統一的步驟延遲系統

重構 `AppSettings.cs` 和 `InputService.cs`:

```csharp
// AppSettings.cs
public class AppSettings
{
    // 現有的細粒度延遲
    public int CharacterSelectionDelayMs { get; set; } = 50;
    public int ServerSelectionDelayMs { get; set; } = 50;
    public int KeyboardInputDelayMs { get; set; } = 100;
    public int MouseClickDelayMs { get; set; } = 200;

    // 新增: 統一的步驟間延遲
    public int StepDelayMs { get; set; } = 500;  // 每個主要步驟之間的延遲
    public int WindowFocusDelayMs { get; set; } = 300;  // 視窗焦點操作後的延遲
    public int WindowReadyTimeoutMs { get; set; } = 5000;  // 等待視窗就緒的超時
}
```

### 方案3: 修正自動選擇邏輯

```csharp
private void SelectServer(int server, bool autoSelectServer, int targetProcessId)
{
    Thread.Sleep(_settings.StepDelayMs);
    EnsureWindowReadyAndFocused(targetProcessId);

    if (autoSelectServer && server > 0)
    {
        // 執行選擇邏輯
        // ...

        // ✅ 只在自動選擇時才按Enter
        EnsureWindowReadyAndFocused(targetProcessId);
        SendKey(Keys.Enter);
    }
    // ✅ 如果不自動選擇,不執行Enter,停在選擇畫面
}
```

### 方案4: 強化批次啟動流程

```csharp
private void LaunchGameForAccountInternal(Account account)
{
    // 1. 啟動遊戲進程
    var gameProcess = Process.Start(processInfo);

    // 2. 等待視窗出現
    var gameWindow = InputService.WaitForRoWindowByPid(gameProcess.Id, ...);

    // 3. ✅ 新增: 等待視窗完全就緒
    if (!_windowReadinessService.WaitForWindowReady(gameWindow, _settings.WindowReadyTimeoutMs))
    {
        throw new Exception("視窗就緒超時");
    }

    // 4. ✅ 新增: 確保視窗獲得焦點
    SetForegroundWindow(gameWindow);
    Thread.Sleep(_settings.WindowFocusDelayMs);

    // 5. ✅ 新增: 最終檢查視窗狀態
    if (!IsWindowReadyForInput(gameWindow, gameProcess.Id))
    {
        throw new Exception("視窗未就緒無法接收輸入");
    }

    // 6. 執行登入
    Dispatcher.Invoke(() => {
        inputService.SendLogin(...);
    });
}
```

## 實施計劃

### 階段1: 核心基礎設施 (優先)
- [ ] 創建 `WindowReadinessService.cs`
- [ ] 擴展 `AppSettings.cs` 的延遲配置
- [ ] 更新 `SettingsWindow.xaml` 添加新的延遲設定UI

### 階段2: InputService 重構 (關鍵)
- [ ] 重構 `SendLogin()` 使用統一延遲
- [ ] 修正 `SelectServer()` 和 `SelectCharacter()` 邏輯
- [ ] 在每個關鍵步驟前添加視窗就緒檢查
- [ ] 所有 `CheckRagnarokWindowFocus()` 替換為 `EnsureWindowReadyAndFocused()`

### 階段3: 批次啟動優化 (重要)
- [ ] 重構 `MainWindow.LaunchGameForAccountInternal()`
- [ ] 添加完整的視窗就緒檢測流程
- [ ] 改進錯誤處理和日誌記錄

### 階段4: 測試與驗證
- [ ] 單個帳號登入測試
- [ ] 批次啟動2-5個帳號測試
- [ ] 批次啟動10+個帳號壓力測試
- [ ] 未勾選自動選擇行為測試

## 預期效果

1. **視窗響應問題解決**: 透過就緒檢測確保視窗可操作後才輸入
2. **等待時間可控**: 透過細粒度延遲設定實現精確控制
3. **自動選擇行為正確**: 未勾選時停在選擇畫面,不會自動進入
4. **批次啟動穩定**: 每個帳號都經過完整的就緒檢查

## 技術細節

### 視窗就緒檢測原理

```csharp
// 1. 檢查視窗可見性
IsWindowVisible(hwnd)

// 2. 檢查視窗響應性
SendMessageTimeout(hwnd, WM_NULL, 0, 0, SMTO_ABORTIFHUNG, 100, out result)

// 3. 檢查視窗繪製完成
GetWindowDC() 檢查DC有效性

// 4. 檢查客戶區大小
GetClientRect() 確保有效的客戶區

// 5. 多次重試確保穩定
for (int i = 0; i < maxRetries; i++) { ... }
```

### 延遲層級設計

```
Level 1: 微操作延遲 (50-200ms)
  - CharacterSelectionDelayMs: 角色選擇方向鍵間隔
  - ServerSelectionDelayMs: 伺服器選擇方向鍵間隔
  - KeyboardInputDelayMs: 鍵盤輸入後等待
  - MouseClickDelayMs: 滑鼠點擊後等待

Level 2: 步驟間延遲 (300-500ms)
  - StepDelayMs: 主要步驟之間的延遲
  - WindowFocusDelayMs: 視窗焦點操作後的延遲

Level 3: 同步等待 (1000-5000ms)
  - OtpInputDelayMs: OTP視窗出現等待
  - WindowReadyTimeoutMs: 視窗就緒超時
```

## 向後兼容性

所有新的延遲設定都提供預設值,不影響現有用戶:
- 現有設定檔會自動使用預設值
- UI會顯示所有設定項供進階用戶調整
- 現有行為保持不變,除非用戶主動調整

## 風險與緩解

**風險1**: 視窗就緒檢測可能增加整體登入時間
**緩解**: 提供配置選項允許調整超時和重試次數

**風險2**: 不同系統/配置的視窗就緒時間差異大
**緩解**: 預設值保守,提供UI讓用戶根據自己系統優化

**風險3**: 現有用戶升級後行為改變
**緩解**: 保持向後兼容,新功能預設關閉或使用保守值
