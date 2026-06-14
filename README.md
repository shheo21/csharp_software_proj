# SubscriptionManager API

구독 서비스를 관리하는 RESTful API입니다. JWT 인증 기반으로 사용자별 구독 등록·조회·수정·삭제와 지출 분석, 알림, 실시간 환율 변환을 제공합니다.

## 기술 스택

- **Runtime**: .NET 10
- **Framework**: ASP.NET Core Web API
- **ORM**: Entity Framework Core 10
- **DB**: PostgreSQL
- **인증**: ASP.NET Core Identity + JWT Bearer + Refresh Token
- **문서화**: Swagger / OpenAPI

---

## 시작하기

### 사전 요구 사항

- .NET 10 SDK

### 환경 설정

`appsettings.Development.json` 파일을 프로젝트 루트에 생성합니다.

```json
{
  "Jwt": {
    "Key": "32자_이상의_시크릿_키"
  },
  "ExchangeRate": {
    "ApiKey": "한국수출입은행_API_키"
  }
}
```

> 한국수출입은행 API 키는 https://www.koreaexim.go.kr 에서 발급받을 수 있습니다.

### 실행

```bash
dotnet run --project SubscriptionManager.api
```

서버 기동 시 DB가 자동 생성되고, 테스트 계정과 Mock 데이터가 시드됩니다.

| 항목 | 값 |
|---|---|
| 테스트 이메일 | `test@example.com` |
| 테스트 비밀번호 | `Test1234!` |
| Swagger UI | `https://localhost:{port}/swagger` |

---

## API 목록

### 🔐 인증 (`/api/auth`)

| 메서드 | 엔드포인트 | 인증 | 설명 |
|---|---|---|---|
| POST | `/api/auth/register` | 불필요 | 회원가입 |
| POST | `/api/auth/login` | 불필요 | 로그인 — Access Token + Refresh Token 반환 |
| POST | `/api/auth/refresh` | 불필요 | Access Token 재발급 (Refresh Token Rotation) |
| GET | `/api/auth/me` | 필요 | 내 계정 정보 조회 |
| POST | `/api/auth/logout` | 필요 | 로그아웃 — Refresh Token 폐기 |

**Access Token 만료**: 15분 / **Refresh Token 만료**: 30일

---

### 📋 구독 관리 (`/api/subscriptions`)

> 모든 엔드포인트 인증 필요

| 메서드 | 엔드포인트 | 설명 |
|---|---|---|
| GET | `/api/subscriptions` | 구독 목록 조회 (검색·필터 지원) |
| POST | `/api/subscriptions` | 새 구독 등록 |
| PUT | `/api/subscriptions/{id}` | 구독 정보 수정 |
| DELETE | `/api/subscriptions/{id}` | 구독 삭제 |

**Query Parameters** (`GET /api/subscriptions`)

| 파라미터 | 타입 | 설명 |
|---|---|---|
| `search` | string | 구독명·카테고리 키워드 검색 |
| `category` | string | 카테고리 필터 |
| `currency` | string | 통화 코드 필터 (예: USD) |

**결제 주기**: `MONTHLY` / `YEARLY`

응답에는 KRW 환산 금액(`amountInKRW`), 월 환산 금액(`monthlyAmountInKRW`), 다음 결제까지 남은 일수(`daysUntilBilling`)가 포함됩니다.

---

### 📊 대시보드 (`/api/subscriptions/dashboard`)

> 인증 필요

| 메서드 | 엔드포인트 | 설명 |
|---|---|---|
| GET | `/api/subscriptions/dashboard` | 월 총 지출, 카테고리별 비중, 결제 임박 구독 조회 |

**응답 필드**

| 필드 | 설명 |
|---|---|
| `totalMonthlyKRW` | 월 총 구독료 (KRW) |
| `totalYearlyKRW` | 연 총 구독료 (KRW) |
| `activeCount` | 활성 구독 수 |
| `upcomingBillingCount` | 7일 이내 결제 예정 수 |
| `upcomingBilling` | 결제 임박 구독 목록 (최대 10개) |
| `categoryBreakdown` | 카테고리별 지출 금액·비중 |

---

### 📈 지출 분석 (`/api/subscriptions/spending-trends`)

> 인증 필요

| 메서드 | 엔드포인트 | 설명 |
|---|---|---|
| GET | `/api/subscriptions/spending-trends` | 월별 지출 트렌드 및 카테고리 분석 |

**Query Parameters**

| 파라미터 | 타입 | 기본값 | 설명 |
|---|---|---|---|
| `months` | int | 12 | 조회 기간 (1~24개월) |

**응답 필드**

| 필드 | 설명 |
|---|---|
| `monthlyTrends` | 월별 총 지출 및 카테고리별 금액 목록 |
| `categoryBreakdown` | 카테고리별 월 평균 지출·비중 |
| `totalMonthlyKRW` | 현재 월 총 구독료 |
| `topCategory` | 지출 1위 카테고리 |
| `averageMonthlyKRW` | 기간 내 월 평균 지출 |

연간 구독은 청구 월에만 해당 금액이 반영됩니다.

---

### 🔔 알림 (`/api/notifications`)

> 모든 엔드포인트 인증 필요

| 메서드 | 엔드포인트 | 설명 |
|---|---|---|
| GET | `/api/notifications` | 알림 목록 조회 |
| GET | `/api/notifications/unread-count` | 읽지 않은 알림 수 조회 |
| PATCH | `/api/notifications/{id}/read` | 특정 알림 읽음 처리 |
| PATCH | `/api/notifications/read-all` | 전체 알림 읽음 처리 |

**Query Parameters** (`GET /api/notifications`)

| 파라미터 | 타입 | 기본값 | 설명 |
|---|---|---|---|
| `unreadOnly` | bool | false | 읽지 않은 알림만 조회 |

---

### 💱 환율 (`/api/exchangerate`)

> 인증 불필요

| 메서드 | 엔드포인트 | 설명 |
|---|---|---|
| GET | `/api/exchangerate` | 지원 통화 전체 환율 조회 (알파벳 순) |
| GET | `/api/exchangerate/{currencyCode}` | 특정 통화의 KRW 환율 조회 |
| POST | `/api/exchangerate/refresh` | 환율 캐시 강제 초기화 |

환율 데이터는 **한국수출입은행 API**에서 가져오며 **1시간** 동안 캐시됩니다. API 장애 또는 비영업일의 경우 DB에 저장된 마지막 환율이 사용됩니다. 영업일 11시 이전 호출 시 최대 7일 이전 데이터까지 자동으로 재시도합니다.

지원하지 않는 통화 코드 조회 시 `rateToKRW: 1.0`을 반환합니다.

---

## 프로젝트 구조

```
SubscriptionManager.api/
├── Controllers/
│   ├── AuthController.cs
│   ├── SubscriptionsController.cs
│   ├── DashboardController.cs
│   ├── SpendingAnalysisController.cs
│   ├── NotificationsController.cs
│   └── ExchangeRateController.cs
├── Services/
│   ├── AuthService.cs
│   ├── SubscriptionService.cs
│   ├── DashboardService.cs
│   ├── SpendingAnalysisService.cs
│   ├── NotificationService.cs
│   ├── SubscriptionCalculationService.cs
│   └── ExchangeRateService.cs
├── Models/
│   ├── ApplicationUser.cs
│   ├── Subscription.cs
│   ├── AuthModels.cs
│   ├── SubscriptionModels.cs
│   ├── AnalyticsModels.cs
│   └── Notification.cs
├── Data/
│   └── AppDbContext.cs
└── Program.cs
```

# SubscriptionManager Blazor

구독 관리 웹페이지를 제공하는 Blazor 프로젝트입니다.

## 프론트엔드 API 설정

Blazor WASM은 `SubscriptionManager.blazor/wwwroot/appsettings.json`의 `ApiBaseUrl` 값을 읽어 백엔드 API 기본 주소로 사용합니다.

```json
{
  "ApiBaseUrl": "${API_BASE_URL}"
}
```

배포용 `appsettings.json`은 placeholder를 유지하고, GitHub Pages workflow에서 빌드 전에 `API_BASE_URL` 값으로 치환합니다. `API_BASE_URL`은 비밀값이 아니므로 GitHub Actions Secret보다 Variable을 권장합니다.

```yaml
- name: Inject API base URL
  shell: bash
  env:
    API_BASE_URL: ${{ vars.API_BASE_URL }}
  run: |
    sed -i "s|\${API_BASE_URL}|$API_BASE_URL|g" SubscriptionManager.blazor/wwwroot/appsettings.json
```

GitHub Actions Variable 예시:

```text
API_BASE_URL=https://subscriptionmanager-api.onrender.com
```

로컬 개발에서는 `SubscriptionManager.blazor/wwwroot/appsettings.Development.json`이 `https://localhost:7188`을 사용합니다.
