# StampIQ Unity Game API

How a Unity game kiosk identifies a visitor, runs one play, and sends the result to StampIQ.

---

## 1. Basics

| | |
|---|---|
| **Base URL** | `https://api.stampiq.sa/api/v1/games` |
| **Auth** | Header `Authorization: Bearer gk_…` (the game API key StampIQ gives you) |
| **Format** | JSON in, JSON out. Send `Content-Type: application/json` and `Accept: application/json` |
| **Times** | ISO 8601 with offset, e.g. `2026-10-02T09:03:16+03:00` |
| **Rate limits** | 120 requests/minute per key; `qr/validate` 30/minute per key + IP. Over the limit → `429 RATE_LIMITED` |

A key belongs to **one event** and may be limited to some games. Everything it sees (games, visitors, sessions) is inside that event.

### Every response has the same envelope

Success:
```json
{ "success": true, "message": "…", "data": { … } }
```

Error:
```json
{
  "success": false,
  "message": "Human-readable text you can show on screen",
  "error_code": "MACHINE_READABLE_CODE",
  "errors": null,
  "details": { "status": "created", "next_action": "submit_pre_questionnaire" }
}
```
- Branch on `error_code`, never on `message` (the wording can change).
- `errors` is filled for validation problems: `{ "field": ["reason"] }`.
- `details` is present on order errors: it tells you where the session is and what to call next.

---

## 2. The flow

```
 [scan QR / type code] ──► 1. POST /qr/validate        → visitor name + scan_token
                           2. POST /sessions           → session_id
                           3. GET  /sessions/{id}/pre-questionnaire
                           4. POST /sessions/{id}/pre-questionnaire
                           5. POST /sessions/{id}/start      ◄── gameplay begins
                           6. POST /sessions/{id}/result     ◄── gameplay ends
                           7. GET  /sessions/{id}/post-questionnaire
                           8. POST /sessions/{id}/post-questionnaire
                           9. POST /sessions/{id}/complete   → before/after scores to show
```

- If the game has **no questionnaire** (`pre_questionnaire_enabled: false` in the config), skip steps 3, 4, 7 and 8.
- Steps must be done **in order**. A step called too early returns an error with `details.next_action`.
- **Every response for a session includes `next_action`**: follow it and you can't go wrong.
- Visitor walks away? Call `POST /sessions/{id}/abandon`.
- **Replays are unlimited.** Scanning the same visitor again starts a new play (`play_number` 2, 3, …). An unfinished earlier play is closed automatically.

---

## 3. Endpoints

### 3.0 `GET /` — games this key can use
Optional; useful at kiosk start-up.

**Response 200**
```json
{
  "success": true,
  "message": "Games retrieved",
  "data": {
    "games": [ { …same object as 3.0b… } ]
  }
}
```

### 3.0b `GET /{game_code}/config` — one game's settings

**Response 200**
```json
{
  "success": true,
  "message": "Game configuration retrieved",
  "data": {
    "game_code": "energy-grid",
    "name_en": "Energy Grid",
    "name_ar": "شبكة الطاقة",
    "description_en": "Balance the grid before it overloads.",
    "description_ar": null,
    "questionnaire_id": 12,
    "pre_questionnaire_enabled": true,
    "post_questionnaire_enabled": true,
    "question_count": 2,
    "replay_allowed": true,
    "result_fields": [
      { "key": "level", "label_en": "Level reached", "label_ar": null, "type": "number" },
      { "key": "correct_answers", "label_en": "Correct answers", "label_ar": null, "type": "number" }
    ],
    "result_email_enabled": true
  }
}
```
`result_fields` lists the only keys allowed inside `stats` when you submit the result (step 6). If it is empty, any stats are accepted.

---

### 3.1 `POST /qr/validate` — identify the visitor

Send **one** of:
- `qr_code`: the text read from the visitor's QR (badge or ticket QR, full URL or bare code), **or**
- `ticket_number`: what the visitor **typed**:
  - their ticket number, e.g. `T-2032` (or just `2032`), or
  - at events without tickets, their **5-digit game code** from their email, e.g. `48271`.

**Request (scanned)**
```json
{ "qr_code": "https://api.stampiq.sa/t/T-2032", "game_code": "energy-grid" }
```

**Request (typed)**
```json
{ "ticket_number": "48271", "game_code": "energy-grid" }
```

**Response 200**
```json
{
  "success": true,
  "message": "Ticket number valid",
  "data": {
    "scan_token": "eyJ2IjoxLCJjIjo…IMYm_pYka4TwiCYHW9-CemYK7QgrXSWyZ6Lp1WA4fZw",
    "scan_token_expires_at": "2026-10-02T09:13:16+03:00",
    "scan_source": "player_code",
    "game_code": "energy-grid",
    "player": {
      "display_name": "Sara Ahmed",
      "first_name": "Sara",
      "email_masked": "s***@example.com"
    },
    "plays_completed": 0,
    "open_session_id": null
  }
}
```
- `message` is `"QR code valid"` for a scan, `"Ticket number valid"` for a typed number.
- `scan_source`: `badge` | `ticket` (scanned), `ticket_number` | `player_code` (typed).
- Show `player.first_name` / `display_name` on screen ("Welcome, Sara!").
- `scan_token` is **valid for 10 minutes** and opens **one** session. Pass it to step 2 as-is.
- `open_session_id`: if not null, this visitor has an unfinished play. You can resume it with `GET /sessions/{id}`, or simply create a new session (the old one is closed automatically).

**Errors:** `INVALID_QR_CODE` (404), `INVALID_TICKET_NUMBER` (404), `TICKET_CANCELLED` (403), `TICKET_NOT_LINKED` (422), `INVALID_GAME` (404), `VALIDATION_ERROR` (422: neither or both of `qr_code`/`ticket_number`).

---

### 3.2 `POST /sessions` — start a play

**Request**
```json
{ "game_code": "energy-grid", "scan_token": "eyJ2IjoxLCJjIjo…" }
```

**Response 201**
```json
{
  "success": true,
  "message": "Game session created",
  "data": {
    "session_id": "eoi0ocjvs7qziZ8IMYTFNzVgV3tZ3XKq",
    "game_code": "energy-grid",
    "status": "created",
    "next_action": "submit_pre_questionnaire",
    "play_number": 1,
    "scan_source": "player_code",
    "questionnaire_enabled": true,
    "player": { "display_name": "Sara Ahmed", "first_name": "Sara", "email_masked": "s***@example.com" },
    "timestamps": {
      "created_at": "2026-10-02T09:03:16+03:00",
      "pre_completed_at": null,
      "started_at": null,
      "result_submitted_at": null,
      "post_completed_at": null,
      "completed_at": null,
      "abandoned_at": null
    },
    "result": null,
    "knowledge": null,
    "email_status": null,
    "reused": false
  }
}
```
This object is the **session object**. Steps 5, 6, 9, `GET /sessions/{id}` and `abandon` all return it (plus one extra flag each).

- **Safe to retry:** sending the same `scan_token` again (e.g. after a network timeout) returns the **same** session with `"reused": true` and status 200.
- **Errors:** `SCAN_TOKEN_EXPIRED` (410), `SCAN_TOKEN_INVALID` (422), `SCAN_TOKEN_USED` (409). In all three cases, scan again.

---

### 3.3 `GET /sessions/{session_id}/pre-questionnaire`

**Response 200**
```json
{
  "success": true,
  "message": "Questionnaire retrieved",
  "data": {
    "session_id": "eoi0ocjvs7qziZ8IMYTFNzVgV3tZ3XKq",
    "questionnaire_id": 12,
    "phase": "pre",
    "submitted": false,
    "questions": [
      {
        "id": 2964,
        "question_en": "How much do you know about renewable energy?",
        "question_ar": "ما مدى معرفتك بالطاقة المتجددة؟",
        "type": "rating",
        "min": 0,
        "max": 10,
        "required": true
      },
      {
        "id": 2965,
        "question_en": "How confident are you about saving energy at home?",
        "question_ar": "ما مدى ثقتك في توفير الطاقة في المنزل؟",
        "type": "rating",
        "min": 0,
        "max": 10,
        "required": true
      }
    ]
  }
}
```
Show each question with a scale from `min` to `max`.

### 3.4 `POST /sessions/{session_id}/pre-questionnaire`

**Request:** one entry per question; `answer` is a whole number between `min` and `max`.
```json
{
  "answers": [
    { "question_id": 2964, "answer": 4 },
    { "question_id": 2965, "answer": 5 }
  ]
}
```

**Response 200**
```json
{
  "success": true,
  "message": "Pre-game answers saved",
  "data": {
    "session_id": "eoi0ocjvs7qziZ8IMYTFNzVgV3tZ3XKq",
    "status": "pre_completed",
    "next_action": "start_game",
    "answers_saved": 2
  }
}
```
**Errors:** `INVALID_ANSWER` (422: out of range or unknown question), `MISSING_REQUIRED_ANSWER` (422), `QUESTIONNAIRE_ALREADY_SUBMITTED` (409).

---

### 3.5 `POST /sessions/{session_id}/start` — gameplay begins
No body.

**Response 200:** the session object with `"status": "playing"`, `"next_action": "submit_result"`, and `"already_started": false`.

Calling it again is harmless (`"already_started": true`).

**Error:** `MISSING_QUESTIONNAIRE` (422) if the pre-questionnaire wasn't submitted:
```json
{
  "success": false,
  "message": "Submit the pre-game questionnaire before starting the game.",
  "error_code": "MISSING_QUESTIONNAIRE",
  "errors": null,
  "details": { "status": "created", "next_action": "submit_pre_questionnaire" }
}
```

---

### 3.6 `POST /sessions/{session_id}/result` — gameplay ends

**Request**
```json
{
  "score": 850,
  "completion_time": 125,
  "attempts": 1,
  "game_status": "completed",
  "stats": { "level": 3, "correct_answers": 18 }
}
```
| Field | Required | Type | Notes |
|---|---|---|---|
| `score` | yes | number | |
| `completion_time` | no | integer | seconds, 0–86400 |
| `attempts` | no | integer | |
| `game_status` | no | string | `completed` (default) · `failed` · `timeout` · `quit` |
| `stats` | no | object | keys must be in the game's `result_fields` (if any); values number / text / true-false |

**Response 200:** the session object with `"status": "result_submitted"`, a filled `result`, and `"duplicate": false`:
```json
"result": {
  "score": 850,
  "completion_time": 125,
  "attempts": 1,
  "game_status": "completed",
  "stats": { "level": 3, "correct_answers": 18 }
}
```
- **Safe to retry:** sending the **identical** result again returns 200 with `"duplicate": true`.
- **Errors:** a **different** result returns `RESULT_ALREADY_SUBMITTED` (409); `INVALID_RESULT` (422: bad stats); `INVALID_SESSION_STATE` (409: `start` not called).

---

### 3.7 / 3.8 Post-game questionnaire
Same as 3.3 / 3.4, at `/sessions/{session_id}/post-questionnaire`. The questions are the same as before the game; `phase` is `"post"`.

**POST response 200**
```json
{
  "success": true,
  "message": "Post-game answers saved",
  "data": {
    "session_id": "eoi0ocjvs7qziZ8IMYTFNzVgV3tZ3XKq",
    "status": "post_completed",
    "next_action": "complete",
    "answers_saved": 2
  }
}
```

---

### 3.9 `POST /sessions/{session_id}/complete` — finish, show the result
No body.

**Response 200:** the session object with `"status": "completed"`, `"next_action": null`, `"already_completed": false`, and the before/after comparison in `knowledge`:
```json
"knowledge": {
  "pre_score": 9,
  "post_score": 15,
  "max_score": 20,
  "score_change": 6,
  "percentage_change": 66.67,
  "knowledge_result": "improved",
  "questions": [
    {
      "question_id": 2964,
      "pre_score": 4,
      "post_score": 8,
      "change": 4,
      "percentage_change": 100,
      "question_en": "How much do you know about renewable energy?",
      "question_ar": "ما مدى معرفتك بالطاقة المتجددة؟",
      "min_score": 0,
      "max_score": 10
    }
  ]
},
"email_status": "queued"
```
- `knowledge_result`: `improved` | `same` | `decreased`.
- `percentage_change` is `null` when the before score was 0.
- `knowledge` is `null` for games without a questionnaire.
- `email_status`: `queued` (the visitor will be emailed their result) or `null` (result emails are off for this game).
- Calling `complete` again is harmless (`"already_completed": true`).

---

### 3.10 `GET /sessions/{session_id}` — current state
Returns the session object. Use it to resume after a crash or restart.

### 3.11 `POST /sessions/{session_id}/abandon` — visitor left
No body. Returns the session object with `"status": "abandoned"` and `"already_abandoned": false` (true if it was already closed). After this, the session can't be used; the visitor scans again to play.

A session nobody touches for **3 hours** is closed automatically (`SESSION_EXPIRED`).

---

## 4. Session status and `next_action`

| `status` | `next_action` | Call next |
|---|---|---|
| `created` | `submit_pre_questionnaire` (or `start_game` with no questionnaire) | 3.4 (or 3.5) |
| `pre_completed` | `start_game` | 3.5 |
| `playing` | `submit_result` | 3.6 |
| `result_submitted` | `submit_post_questionnaire` (or `complete` with no questionnaire) | 3.8 (or 3.9) |
| `post_completed` | `complete` | 3.9 |
| `completed` | `null` | done; show the result |
| `abandoned` | `null` | closed; scan again |

---

## 5. Error codes

| `error_code` | HTTP | Meaning | What the kiosk should do |
|---|---|---|---|
| `UNAUTHORIZED` | 401 | Key missing, wrong, revoked or expired | Check the key configuration |
| `FORBIDDEN` | 403 | Key not allowed (e.g. IP restriction) | Contact StampIQ |
| `MODULE_DISABLED` | 403 | Game system switched off for the event | Contact StampIQ |
| `INVALID_PROJECT` | 403 | The key's event is unavailable | Contact StampIQ |
| `INVALID_GAME` | 404 | Unknown game code, or not allowed for this key | Check `game_code` |
| `INVALID_QR_CODE` | 404 | QR not recognised for this event | "Please scan again" |
| `INVALID_TICKET_NUMBER` | 404 | Typed number/code not found | "Check the number and try again" |
| `TICKET_CANCELLED` | 403 | Ticket was cancelled | Show the message |
| `TICKET_NOT_LINKED` | 422 | Guest ticket with no visitor record | "Please scan your badge" |
| `REGISTRANT_NOT_FOUND` | 404 | Visitor no longer exists | Scan again |
| `SCAN_TOKEN_INVALID` | 422 | Token damaged or for another game/key | Scan again |
| `SCAN_TOKEN_EXPIRED` | 410 | Token older than 10 minutes | Scan again |
| `SCAN_TOKEN_USED` | 409 | Token already used | Scan again |
| `SESSION_NOT_FOUND` | 404 | Unknown session id | Scan again |
| `SESSION_EXPIRED` | 410 | Untouched for 3 hours, closed | Scan again |
| `SESSION_ABANDONED` | 409 | Session was closed | Scan again |
| `SESSION_ALREADY_COMPLETED` | 409 | Already finished | Show the result (`GET /sessions/{id}`) |
| `INVALID_SESSION_STATE` | 409 | Step called out of order | Call `details.next_action` |
| `MISSING_QUESTIONNAIRE` | 422 | Questionnaire not submitted yet | Call `details.next_action` |
| `MISSING_GAME_RESULT` | 422 | Result not submitted yet | Call `details.next_action` |
| `QUESTIONNAIRE_DISABLED` | 409 | This game has no questionnaire | Skip questionnaire steps |
| `QUESTIONNAIRE_ALREADY_SUBMITTED` | 409 | Answers already saved | Move to `next_action` |
| `QUESTIONNAIRE_NOT_FOUND` | 404 | No questions for this session | Skip questionnaire steps |
| `INVALID_ANSWER` | 422 | Answer out of range / unknown question | Fix and resend (see `errors`) |
| `MISSING_REQUIRED_ANSWER` | 422 | A required question is missing | Fix and resend |
| `INVALID_RESULT` | 422 | Bad `stats` | Fix and resend (see `errors`) |
| `RESULT_ALREADY_SUBMITTED` | 409 | A different result was already saved | Move on to `next_action` |
| `VALIDATION_ERROR` | 422 | Missing/invalid fields | Fix and resend (see `errors`) |
| `NOT_FOUND` | 404 | Wrong URL | Check the endpoint |
| `METHOD_NOT_ALLOWED` | 405 | Wrong HTTP method | Check the method |
| `RATE_LIMITED` | 429 | Too many requests | Wait and retry |
| `SERVER_ERROR` | 500 | Unexpected server error | Retry; report if it persists |

---

## 6. Tips for the kiosk

- **Retries are safe.** `POST /sessions` (same `scan_token`), `start`, `result` (identical body), `complete` and `abandon` can all be repeated after a timeout without side effects.
- **Keep `session_id`** in memory for the whole play; after a crash, `GET /sessions/{id}` tells you where you were.
- **Never invent a visitor.** A session can only be opened with a `scan_token` from `qr/validate`.
- **Show `message`** to the visitor on errors; it is written for them. Decide what to do from `error_code`.
- **Test with Postman first:** the StampIQ Unity Game API collection runs this whole flow (requests 1a/1b → 9).
