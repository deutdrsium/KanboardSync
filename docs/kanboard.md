# 同步到 Kanboard 部署指南

本指南介绍如何将 Canvas LMS 的作业、测验、公告、讨论同步到自托管的 [Kanboard](https://kanboard.org/) 实例。

---

## 前置条件

- 已部署并可访问的 Kanboard 实例（支持 v1.2.x 及以上版本）
- Kanboard 已启用 API（默认启用）
- 本程序的可执行文件（从 [Releases](../../../releases) 下载对应平台的版本，或自行编译）

---

## 一、Canvas 令牌

1. 打开 Canvas，进入右上角菜单 → **设置**页面

2. 滚动到页面底部，找到"已批准的集成"，点击"创建新访问许可证"

3. "用途"随便填写，"过期"建议**留空**，点击"生成令牌"

4. 复制生成的令牌**备用**（此令牌只显示一次）

---

## 二、获取 Kanboard API Token

5. 以管理员身份登录 Kanboard，进入：  
   右上角用户菜单 → **设置** → **API**

6. 复制页面上显示的 **API endpoint** 地址（形如 `http://your-server/jsonrpc.php`）和 **API token**

   > 也可以使用用户级别的 API Token（在用户资料页面生成），但推荐使用全局 API Token 以确保有权限创建分类和任务。

---

## 三、在 Kanboard 中创建项目

7. 点击顶部导航"项目" → "新建项目"，填写项目名称（例如 `Canvas`），创建项目

8. 记录项目 ID（可在项目设置页面的 URL 中找到，例如 `https://kanboard.example.com/?controller=ProjectViewController&action=show&project_id=`**`1`**）

9. （可选）记录"待办"列的列 ID：在项目看板页面，列标题右键菜单或列设置中可以看到列 ID；若填 `0` 则程序自动使用项目第一列

---

## 四、配置 config.yaml

10. 用文本编辑器打开程序目录下的 `config.yaml`，做如下修改：

    ```yaml
    # 将同步目标改为 Kanboard
    TargetService : Kanboard

    # 填写 Kanboard 相关配置
    KanboardConfig :
      Url : "http://your-kanboard-server"   # 末尾不带斜杠
      ApiToken : "your-api-token-here"
      ProjectId : 1                         # 第三步记录的项目 ID
      DefaultColumnId : 0                   # 0 = 自动使用项目第一列
      AssignmentCategoryName : "Canvas 作业"
      QuizCategoryName : "Canvas 测验"
      DiscussionCategoryName : "Canvas 讨论"
      AnouncementCategoryName : "Canvas 公告"
      NotificationCategoryName : "Canvas 通知"
    ```

    > 五个 CategoryName 可以自由修改。如果对应的分类在 Kanboard 中不存在，程序会自动创建。

---

## 五、配置 token.json（本地运行）

11. 用文本编辑器打开程序目录下的 `token.json`，按如下格式填写：

    ```json
    {
      "CanvasToken": "这里填上你的 Canvas 令牌",
      "KanboardCredential": {
        "Url": "http://your-kanboard-server",
        "ApiToken": "your-api-token-here"
      }
    }
    ```

    > `KanboardCredential` 中的 `Url` 和 `ApiToken` 优先级高于 `config.yaml` 中的设置，若两处都填写则以 `token.json` 为准。

---

## 六、配置定时任务（本地运行）

### Windows

12. 搜索"任务计划程序"，打开，在任意文件夹内**创建任务**

13. 触发器：按需设置（建议每小时触发一次）

14. 操作 → 程序：`TodoSynchronizer.CLI.exe`；参数：
    ```
    -local
    ```
    （`-local` 模式会自动读取同目录下的 `config.yaml` 和 `token.json`）

    若不想使用本地模式，也可以直接传参：
    ```
    -canvastoken <你的Canvas令牌> -configfile <config.yaml路径> -kanboardurl <Kanboard地址> -kanboardtoken <API Token>
    ```

### Linux / 服务器

15. 终端运行 `crontab -e`，添加一行：

    ```cron
    0 * * * * /path/to/TodoSynchronizer.CLI -local
    ```

    或直接传参（适合 GitHub Actions / Docker 等环境）：

    ```bash
    /path/to/TodoSynchronizer.CLI \
      -canvastoken "$CANVAS_TOKEN" \
      -configfile /path/to/config.yaml \
      -kanboardurl "$KANBOARD_URL" \
      -kanboardtoken "$KANBOARD_TOKEN"
    ```

16. 保存退出，运行 `crontab -l` 确认已保存

---

## 七、GitHub Actions 方式（CI 自动同步）

17. Fork 本仓库（取消勾选"Copy the master branch only"）

18. 进入仓库 **Settings → Secrets → Actions**，添加以下 Secret：

    | Name | Value |
    |---|---|
    | `CANVAS_TOKEN` | 第一步获取的 Canvas 令牌 |
    | `KANBOARD_URL` | 你的 Kanboard 地址（如 `http://your-server`） |
    | `KANBOARD_TOKEN` | 第六步获取的 Kanboard API Token |

19. 仓库中已包含专用的工作流文件 `.github/workflows/linux-kanboard.yml`，无需修改，直接使用即可。若其他工作流（`linux.yml` 同步到 MS Todo、`linux-dida.yml` 同步到滴答清单）不需要，可在 Actions 页面将其禁用。

20. 在 Actions 页面左侧选择"**同步到 Kanboard**"，右侧点击"Run workflow"手动触发一次，检查输出是否成功

---

## 数据映射关系

| Canvas 内容 | Kanboard 对象 | 说明 |
|---|---|---|
| 作业 / 测验 / 讨论 / 公告 / 通知 | **Task**（任务） | 每条 Canvas 条目对应一个 Kanboard 任务 |
| 内容类别（作业/测验…） | **Category**（分类） | 通过 `config.yaml` 中的 `*CategoryName` 配置 |
| 提交状态、分数、评论 | **Subtask**（子任务） | 每条提交/评论信息对应一个子任务 |
| Canvas 条目 URL | **Comment**（评论） | 用于唯一标识任务，防止重复创建 |
| 截止时间 | `date_due` | 遵循 `config.yaml` 中的 `DueDateMode` 设置 |

所有 Canvas 四类内容（作业、测验、讨论、公告）统一同步到**同一个 Kanboard 项目**的**同一列**，通过**分类（Category）**加以区分，便于在看板中按分类过滤。

---

## Q&A

#### Kanboard 实例需要公网可访问吗？
- **本地运行 / crontab**：不需要，只要运行同步程序的机器能访问到 Kanboard 即可（局域网也可以）。
- **GitHub Actions**：需要，GitHub 的服务器需要能访问到你的 Kanboard 实例。可以通过 frp / ngrok / Cloudflare Tunnel 等工具暴露到公网。

#### 某个 Canvas 条目被删除后，Kanboard 里的任务会被删除吗？
不会。程序只做创建和更新，不会删除 Kanboard 中的任务。

#### 已有的任务被我手动修改了，下次同步会被覆盖吗？
取决于 `config.yaml` 中各 `UpdateXXX` 的设置。将对应字段设置为 `false` 即可保护你的修改不被覆盖。

#### Canvas Token 泄露有什么风险？
Canvas Token 拥有与你的账号相同的权限，泄露后他人可读取你能看到的所有 Canvas 内容。建议不要将 `token.json` 提交到公开代码仓库。
