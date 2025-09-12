# BIDashboardBackend 測試專案

本專案包含 BI Dashboard Backend 系統的單元測試和整合測試。

## 🧪 測試範圍

### 1. 認證相關測試 (Auth)

#### 1.1 AuthController 測試
- **OAuth 登入功能**
  - ✅ 有效 Firebase Token 登入成功
  - ✅ 無效 Firebase Token 登入失敗
  - ✅ 空 Token 處理
  - ✅ 異常處理

- **刷新權杖功能**
  - ✅ 有效刷新權杖換取新 Token
  - ✅ 無效刷新權杖處理
  - ✅ 過期刷新權杖處理
  - ✅ 異常處理

- **登出功能**
  - ✅ 成功登出並撤銷刷新權杖
  - ✅ 無效刷新權杖處理
  - ✅ 異常處理

#### 1.2 AuthService 測試
- **OAuth 登入邏輯**
  - ✅ Firebase Token 驗證失敗處理
  - ✅ 無效 Token 處理
  - ✅ 空 Token 處理
  - ⚠️ **注意**：成功登入場景需要真實 Firebase API，建議使用整合測試

- **刷新權杖邏輯**
  - ✅ 刷新權杖驗證
  - ✅ 新 Token 生成
  - ✅ 快取更新
  - ✅ 過期處理

- **登出邏輯**
  - ✅ 刷新權杖撤銷
  - ✅ 快取清理
  - ✅ 容錯處理

#### 1.3 JwtTokenService 測試
- **存取權杖管理**
  - ✅ 存取權杖生成
  - ✅ 自訂生命週期
  - ✅ 額外 Claims 支援
  - ✅ 用戶資訊提取

- **刷新權杖管理**
  - ✅ 刷新權杖生成
  - ✅ 隨機性驗證
  - ✅ Base64 格式驗證

## 🛠️ 測試工具

- **測試框架**：NUnit 3.14.0
- **Mock 框架**：Moq 4.20.69
- **斷言庫**：FluentAssertions 6.12.0
- **整合測試**：Microsoft.AspNetCore.Mvc.Testing 8.0.0
- **內存資料庫**：Microsoft.EntityFrameworkCore.InMemory 8.0.0
- **Firebase 支援**：FirebaseAdmin 2.4.0

## 🚀 執行測試

### 執行所有測試
```bash
dotnet test
```

### 執行特定認證測試
```bash
# 執行所有認證相關測試
dotnet test --filter "Auth"

# 執行特定測試類別
dotnet test --filter "AuthControllerTests"
dotnet test --filter "AuthServiceTests"  
dotnet test --filter "JwtTokenServiceTests"
```

### 生成測試覆蓋率報告
```bash
dotnet test --collect:"XPlat Code Coverage"
```

### 執行測試並生成詳細報告
```bash
dotnet test --logger "trx;LogFileName=test-results.trx"
```

## 📋 測試命名規範

```csharp
// 格式：MethodName_Scenario_ExpectedResult
[Test]
public async Task OauthLogin_WithValidRequest_ShouldReturnOkResult()

[Test]
public async Task RefreshTokenAsync_WithInvalidToken_ShouldReturnInvalidTokenResult()

[Test]
public void Generate_WithNullUser_ShouldThrowNullReferenceException()
```

## 🔄 持續整合

- **自動化測試**：每次提交自動執行
- **測試報告**：生成詳細的測試報告
- **覆蓋率報告**：監控測試覆蓋率
- **品質閘門**：測試失敗時阻止部署

## ⚠️ 注意事項

### Firebase API 限制
由於 `AuthService.OauthLogin` 方法內部調用真實的 Firebase API，以下測試場景無法在單元測試中模擬：
- 成功的 Firebase Token 驗證
- 用戶創建和更新邏輯
- JWT 和刷新權杖的完整生成流程

**建議解決方案**：
1. 重構 `AuthService` 以支援依賴注入
2. 創建 `IFirebaseAuthService` 介面
3. 使用整合測試來測試完整的認證流程

### 測試數據
- 所有測試使用模擬數據，不會影響真實資料庫
- JWT 密鑰使用測試專用的安全密鑰
- Redis 使用測試前綴避免與生產環境衝突

## 📊 測試覆蓋率目標

- **程式碼覆蓋率**：≥ 80%
- **分支覆蓋率**：≥ 75%
- **關鍵業務邏輯**：100%

---

**最後更新**：2024年1月
**版本**：1.0
