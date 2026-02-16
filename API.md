# AutoCinema.Pro API 文档

AutoCinema.Pro 现在提供了一个强大的 Web API 接口，允许开发者通过 HTTP 请求远程管理视频生产任务。

## 基础信息

- **基础地址**: `http://localhost:5000` (默认)
- **内容类型**: `application/json`

---

## 终结点概览

| 方法 | 路径 | 描述 |
|------|------|------|
| POST | `/api/videos` | 提交新的视频生成项目 |
| GET | `/api/videos/{id}/progress` | 查询指定项目的进度和当前状态 |
| GET | `/api/videos/{id}/result` | 获取已完成项目的结果详情 |
| GET | `/api/videos` | 列出所有已提交的项目（调试用） |

---

## 接口详细说明

### 1. 提交视频生成项目

创建一个新的视频生产任务。任务提交后将进入后台流水线异步处理。

- **URL**: `/api/videos`
- **方法**: `POST`
- **请求体**:

```json
{
  "projectId": "string",       // 必填。项目唯一标识符
  "title": "string",           // 必填。视频标题
  "rawStoryText": "string",    // 必填。原始故事文本
  "outputDirectory": "string", // 必填。视频文件保存目录
  "baseVisualStyle": "string"  // 可选。视觉风格描述
}
```

- **响应**: `202 Accepted`
- **示例**:

  ```bash
  curl -X POST http://localhost:5000/api/videos \
       -H "Content-Type: application/json" \
       -d '{"projectId":"p001", "title":"测试", "rawStoryText":"生成一个小狗的故事", "outputDirectory":"./output/api"}'
  ```

---

### 2. 查询进度

获取项目的实时运行状态和分阶段进度。

- **URL**: `/api/videos/{id}/progress`
- **方法**: `GET`
- **响应**: `200 OK`
- **响应体模型**:

```json
{
  "projectId": "string",
  "status": "Pending | Processing | Completed | Failed",
  "progress": {
    "stage": "string",      // 当前阶段（如：导演层、演员层、剪辑层）
    "step": "string",       // 具体步骤描述
    "percentage": number,   // 进度百分比 (0-100)
    "currentScene": number, // 当前处理的场景索引
    "totalScenes": number   // 总场景数
  },
  "errorMessage": "string", // 如果失败，显示错误消息
  "createdAt": "datetime",
  "finishedAt": "datetime"
}
```

---

### 3. 获取生成结果

当状态为 `Completed` 时，可以通过此接口获取生成后的文件路径。

- **URL**: `/api/videos/{id}/result`
- **方法**: `GET`
- **响应**: `200 OK` (成功) 或 `400 Bad Request` (未完成)
- **响应体**:

  ```json
  {
    "resultPath": "E:\\...\\output\\demo.mp4",
    "finishedAt": "2026-02-14T15:10:00Z"
  }
  ```

---

## 数据模型说明

### ProjectStatus (枚举)

- `Pending` (0): 等待队列
- `Processing` (1): 正在生产中
- `Completed` (2): 生产成功
- `Failed` (3): 生产失败

---

## 错误处理

API 使用标准的 HTTP 状态码表示结果：

- `202`: 任务已接受并在后台排队。
- `400`: 请求无效或项目状态不支持当前操作。
- `404`: 找不到指定的项目 ID。
- `500`: 服务器内部处理错误。
