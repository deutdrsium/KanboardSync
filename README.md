![](https://s2.loli.net/2022/10/20/2greaNyO7PdvcUh.png)

## 程序简介
基于 GitHub Actions 的定时任务（每小时运行一次同步），将 Canvas LMS 的作业、测验、公告、讨论同步到 **Microsoft Todo / 滴答清单 / 自托管 Kanboard**（目前仅适配上海交通大学 Canvas 系统，理论上所有 Canvas LMS 都能用）

![](https://s2.loli.net/2022/09/14/J8WMPXCvjw34ZOq.png)
![](https://s2.loli.net/2022/10/20/oZjiM92OQtJH1pW.png)

## 支持的同步目标

| 目标 | 认证方式 | 说明 |
|---|---|---|
| Microsoft Todo | OAuth2 刷新令牌 | 需借助 QuickTool 完成授权 |
| 滴答清单 | 手机号 + 密码 | 直接填写账号信息 |
| **Kanboard**（新） | JSON-RPC API Token | 需自行部署 Kanboard 实例 |

## 使用方法
- GitHub Actions 方式（Microsoft Todo / 滴答清单）：[部署指南](/docs/actions-persis.md)
- 本地运行方式（Microsoft Todo / 滴答清单）：[部署指南](/docs/local.md)
- **同步到 Kanboard**：[部署指南](/docs/kanboard.md)

## 特别感谢
- [microsoftgraph/msgraph-sdk-dotnet](https://github.com/microsoftgraph/msgraph-sdk-dotnet)
- [aaubry/YamlDotNet](https://github.com/aaubry/YamlDotNet)
- [moonrailgun/branch-filestorage-action](https://github.com/moonrailgun/branch-filestorage-action)
- [lepoco/wpfui](https://github.com/lepoco/wpfui)
- [TDK1969/nonebot_plugin_dida](https://github.com/TDK1969/nonebot_plugin_dida)
- [kanboard/kanboard](https://github.com/kanboard/kanboard)

## 说在最后
如果觉得程序好用的话，请点亮右上角的 Star 哦~

以及，欢迎Bug Report & Pull Request