using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using TodoSynchronizer.Core.Config;
using TodoSynchronizer.Core.Extensions;
using TodoSynchronizer.Core.Helpers;
using TodoSynchronizer.Core.Models;
using TodoSynchronizer.Core.Models.CanvasModels;
using TodoSynchronizer.Core.Models.KanboardModels;

namespace TodoSynchronizer.Core.Services
{
    public class KanboardSyncService
    {
        /// <summary>Maps Canvas item URL (or notification ID) → existing Kanboard task, identified via task.Reference field.</summary>
        public Dictionary<string, KanboardTask> dicUrl = null;
        /// <summary>Maps category key ("assignment", "quiz", etc.) → Kanboard category ID.</summary>
        public Dictionary<string, int> dicCategoryId = null;
        public List<KanboardTask> canvasTasks = null;
        public List<Course> courses = null;
        public int defaultColumnId = 0;
        public int CourseCount, ItemCount, UpdateCount, FailedCount;

        private string message;
        public string Message
        {
            get => message;
            set
            {
                message = value;
                OnReportProgress?.Invoke(new SyncState(SyncStateEnum.Progress, value));
            }
        }

        public delegate void ReportProgressDelegate(SyncState state);
        public event ReportProgressDelegate OnReportProgress;

        public void Go()
        {
            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                DateTimeZoneHandling = DateTimeZoneHandling.Utc,
                NullValueHandling = NullValueHandling.Ignore
            };
            CourseCount = ItemCount = UpdateCount = FailedCount = 0;

            var kb = SyncConfig.Default.KanboardConfig;
            if (kb == null)
            {
                OnReportProgress?.Invoke(new SyncState(SyncStateEnum.Error, "config.yaml 中未配置 KanboardConfig"));
                return;
            }

            #region 读取 Canvas 课程列表
            Message = "读取 Canvas 课程列表";
            try
            {
                courses = CanvasService.ListCourses();
                if (courses == null)
                    throw new Exception("Canvas 课程列表为空");
            }
            catch (Exception ex)
            {
                OnReportProgress?.Invoke(new SyncState(SyncStateEnum.Error, ex.ToString()));
                return;
            }
            #endregion

            #region 初始化 Kanboard — 列 & 分类
            Message = "初始化 Kanboard";
            try
            {
                defaultColumnId = kb.DefaultColumnId > 0
                    ? kb.DefaultColumnId
                    : KanboardService.GetFirstColumnId(kb.ProjectId);

                var existingCategories = KanboardService.GetAllCategories(kb.ProjectId);
                dicCategoryId = new Dictionary<string, int>();

                int FindOrCreateCategory(string key, string name)
                {
                    var cat = existingCategories.FirstOrDefault(
                        c => string.Equals(c.Name?.CleanEmoji(), name.CleanEmoji(), StringComparison.OrdinalIgnoreCase));
                    if (cat == null)
                    {
                        int newId = KanboardService.CreateCategory(kb.ProjectId, name);
                        Message = $"创建 Kanboard 分类：{name}";
                        return newId;
                    }
                    Message = $"找到 Kanboard 分类：{cat.Name}";
                    return cat.Id;
                }

                if (SyncConfig.Default.NotificationConfig.Enabled)
                    dicCategoryId["notification"] = FindOrCreateCategory("notification", kb.NotificationCategoryName ?? "Canvas 通知");
                if (SyncConfig.Default.AssignmentConfig.Enabled)
                    dicCategoryId["assignment"] = FindOrCreateCategory("assignment", kb.AssignmentCategoryName ?? "Canvas 作业");
                if (SyncConfig.Default.QuizConfig.Enabled)
                    dicCategoryId["quiz"] = FindOrCreateCategory("quiz", kb.QuizCategoryName ?? "Canvas 测验");
                if (SyncConfig.Default.DiscussionConfig.Enabled)
                    dicCategoryId["discussion"] = FindOrCreateCategory("discussion", kb.DiscussionCategoryName ?? "Canvas 讨论");
                if (SyncConfig.Default.AnouncementConfig.Enabled)
                    dicCategoryId["anouncement"] = FindOrCreateCategory("anouncement", kb.AnouncementCategoryName ?? "Canvas 公告");
            }
            catch (Exception ex)
            {
                OnReportProgress?.Invoke(new SyncState(SyncStateEnum.Error, ex.ToString()));
                return;
            }
            #endregion

            #region 读取 Kanboard 任务
            Message = "读取 Kanboard 任务";
            try
            {
                var active = KanboardService.GetAllActiveTasks(kb.ProjectId);
                var closed = KanboardService.GetClosedTasks(kb.ProjectId);
                canvasTasks = active.Concat(closed).ToList();
            }
            catch (Exception ex)
            {
                OnReportProgress?.Invoke(new SyncState(SyncStateEnum.Error, ex.ToString()));
                return;
            }
            #endregion

            #region 建立字典 (reference → task)
            Message = "建立字典";
            dicUrl = new Dictionary<string, KanboardTask>();
            foreach (var task in canvasTasks)
            {
                if (!string.IsNullOrEmpty(task.Reference) && !dicUrl.ContainsKey(task.Reference))
                    dicUrl[task.Reference] = task;
            }
            #endregion

            #region 处理全局通知
            if (SyncConfig.Default.NotificationConfig.Enabled)
                ProcessNotifications();
            #endregion

            #region Main
            try
            {
                foreach (var course in courses)
                {
                    CourseCount++;
                    var prefix = GetCourseMessage(course);
                    if (SyncConfig.Default.AssignmentConfig.Enabled)
                        ProcessAssignments(prefix, course);
                    if (SyncConfig.Default.AnouncementConfig.Enabled)
                        ProcessAnouncements(prefix, course);
                    if (SyncConfig.Default.QuizConfig.Enabled)
                        ProcessQuizes(prefix, course);
                    if (SyncConfig.Default.DiscussionConfig.Enabled)
                        ProcessDiscussions(prefix, course);
                }
            }
            catch (Exception ex)
            {
                OnReportProgress?.Invoke(new SyncState(SyncStateEnum.Error, ex.ToString()));
                return;
            }
            #endregion

            OnReportProgress?.Invoke(new SyncState(
                SyncStateEnum.Finished,
                $"完成！已处理 {CourseCount} 门课程中的 {ItemCount} 个项目，更新 {UpdateCount} 个项目"));
        }

        #region Assignments
        private void ProcessAssignments(string prefix, Course course)
        {
            Message = prefix + "作业";
            try
            {
                var assignments = CanvasService.ListAssignments(course.Id.ToString());
                if (assignments == null || assignments.Count == 0) return;

                foreach (var assignment in assignments)
                {
                    if (assignment.IsQuizAssignment) continue;
                    if (SyncConfig.Default.IgnoreTooOldItems)
                        if (assignment?.DueAt?.ToUniversalTime() < DateTime.Now.AddDays(-14).ToUniversalTime()) continue;

                    ItemCount++;
                    Message = prefix + GetItemMessage(assignment);
                    var catId = dicCategoryId.TryGetValue("assignment", out var cid) ? cid : 0;
                    var task = GetOrNewTask(assignment.HtmlUrl, catId);
                    bool isNew = task.Id == 0;

                    var modified = UpdateCanvasItem(course, assignment, task, SyncConfig.Default.AssignmentConfig);
                    if (modified)
                    {
                        SaveTask(task, assignment.HtmlUrl);
                        UpdateCount++;
                    }

                    // Submissions → Subtasks
                    if (task.Id > 0 && assignment.HasSubmittedSubmissions
                        && (SyncConfig.Default.AssignmentConfig.CreateScoreAndCommit && isNew
                            || SyncConfig.Default.AssignmentConfig.UpdateScoreAndCommit && !isNew))
                    {
                        var subtasks = KanboardService.GetSubtasksByTaskId(task.Id);
                        var submission = CanvasService.GetAssignmentSubmisson(course.Id.ToString(), assignment.Id.ToString());
                        bool subupdated = false;

                        subupdated |= EnsureSubtask(task.Id, subtasks, 0,
                            CanvasStringTemplateHelper.GetSubmissionDesc(assignment, submission));
                        subupdated |= EnsureSubtask(task.Id, subtasks, 1,
                            CanvasStringTemplateHelper.GetGradeDesc(assignment, submission));

                        // Teacher/TA comments
                        if (SyncConfig.Default.AssignmentConfig.CreateComments && isNew
                            || SyncConfig.Default.AssignmentConfig.UpdateComments && !isNew)
                        {
                            if (submission.SubmissionComments?.Count > 0)
                                for (int i = 0; i < submission.SubmissionComments.Count; i++)
                                    subupdated |= EnsureSubtask(task.Id, subtasks, 2 + i,
                                        CanvasStringTemplateHelper.GetSubmissionComment(submission.SubmissionComments[i]));
                        }

                        if (subupdated && !modified) UpdateCount++;
                    }
                }
            }
            catch (Exception ex)
            {
                OnReportProgress?.Invoke(new SyncState(SyncStateEnum.Error, ex.ToString()));
            }
        }
        #endregion

        #region Quizes
        private void ProcessQuizes(string prefix, Course course)
        {
            Message = prefix + "测验";
            try
            {
                var assignments = CanvasService.ListAssignments(course.Id.ToString());
                if (assignments == null || assignments.Count == 0) return;

                foreach (var assignment in assignments)
                {
                    if (!assignment.IsQuizAssignment) continue;
                    if (SyncConfig.Default.IgnoreTooOldItems)
                        if (assignment?.DueAt?.ToUniversalTime() < DateTime.Now.AddDays(-14).ToUniversalTime()) continue;

                    ItemCount++;
                    Message = prefix + GetItemMessage(assignment);
                    var catId = dicCategoryId.TryGetValue("quiz", out var cid) ? cid : 0;
                    var task = GetOrNewTask(assignment.HtmlUrl, catId);
                    bool isNew = task.Id == 0;

                    var modified = UpdateCanvasItem(course, assignment, task, SyncConfig.Default.QuizConfig);
                    if (modified)
                    {
                        SaveTask(task, assignment.HtmlUrl);
                        UpdateCount++;
                    }

                    if (task.Id > 0 && assignment.HasSubmittedSubmissions
                        && (SyncConfig.Default.QuizConfig.CreateScoreAndCommit && isNew
                            || SyncConfig.Default.QuizConfig.UpdateScoreAndCommit && !isNew))
                    {
                        var subtasks = KanboardService.GetSubtasksByTaskId(task.Id);
                        var quizsubmissions = CanvasService.ListQuizSubmissons(course.Id.ToString(), assignment.QuizId.ToString());
                        bool subupdated = false;

                        if (quizsubmissions != null)
                            for (int i = 0; i < quizsubmissions.Count; i++)
                                subupdated |= EnsureSubtask(task.Id, subtasks, i,
                                    CanvasStringTemplateHelper.GetSubmissionDesc(assignment, quizsubmissions[i]));

                        if (subupdated && !modified) UpdateCount++;
                    }
                }
            }
            catch (Exception ex)
            {
                OnReportProgress?.Invoke(new SyncState(SyncStateEnum.Error, ex.ToString()));
            }
        }
        #endregion

        #region Anouncements
        private void ProcessAnouncements(string prefix, Course course)
        {
            Message = prefix + "公告";
            try
            {
                var anouncements = CanvasService.ListAnouncements(course.Id.ToString());
                if (anouncements == null || anouncements.Count == 0) return;

                foreach (var anouncement in anouncements)
                {
                    if (SyncConfig.Default.IgnoreTooOldItems)
                        if (anouncement?.PostedAt?.ToUniversalTime() < DateTime.Now.AddDays(-14).ToUniversalTime()) continue;

                    ItemCount++;
                    Message = prefix + GetItemMessage(anouncement);
                    var catId = dicCategoryId.TryGetValue("anouncement", out var cid) ? cid : 0;
                    var task = GetOrNewTask(anouncement.HtmlUrl, catId);

                    var modified = UpdateCanvasItem(course, anouncement, task, SyncConfig.Default.AnouncementConfig);
                    if (modified)
                    {
                        SaveTask(task, anouncement.HtmlUrl);
                        UpdateCount++;
                    }
                }
            }
            catch (Exception ex)
            {
                OnReportProgress?.Invoke(new SyncState(SyncStateEnum.Error, ex.ToString()));
            }
        }
        #endregion

        #region Discussions
        private void ProcessDiscussions(string prefix, Course course)
        {
            Message = prefix + "讨论";
            try
            {
                var discussions = CanvasService.ListDiscussions(course.Id.ToString());
                if (discussions == null || discussions.Count == 0) return;

                foreach (var discussion in discussions)
                {
                    if (SyncConfig.Default.IgnoreTooOldItems)
                        if (discussion?.PostedAt?.ToUniversalTime() < DateTime.Now.AddDays(-14).ToUniversalTime()) continue;

                    ItemCount++;
                    Message = prefix + GetItemMessage(discussion);
                    var catId = dicCategoryId.TryGetValue("discussion", out var cid) ? cid : 0;
                    var task = GetOrNewTask(discussion.HtmlUrl, catId);

                    var modified = UpdateCanvasItem(course, discussion, task, SyncConfig.Default.DiscussionConfig);
                    if (modified)
                    {
                        SaveTask(task, discussion.HtmlUrl);
                        UpdateCount++;
                    }
                }
            }
            catch (Exception ex)
            {
                OnReportProgress?.Invoke(new SyncState(SyncStateEnum.Error, ex.ToString()));
            }
        }
        #endregion

        #region Notifications
        public void ProcessNotifications()
        {
            Message = "处理全局通知";
            try
            {
                var notifications = CanvasService.ListNotifications();
                if (notifications == null || notifications.Count == 0) return;

                var catId = dicCategoryId.TryGetValue("notification", out var cid) ? cid : 0;

                foreach (var notification in notifications)
                {
                    ItemCount++;
                    Message = "处理全局通知 " + notification.Subject;
                    // Notifications use their numeric ID as reference key (not a URL)
                    var refKey = notification.Id.ToString();
                    var task = GetOrNewTask(refKey, catId);

                    var modified = UpdateCanvasItem(null, notification, task, SyncConfig.Default.NotificationConfig);
                    if (modified)
                    {
                        SaveTask(task, refKey);
                        UpdateCount++;
                    }
                }
            }
            catch (Exception ex)
            {
                OnReportProgress?.Invoke(new SyncState(SyncStateEnum.Error, ex.ToString()));
            }
        }
        #endregion

        #region Common helpers

        private KanboardTask GetOrNewTask(string refKey, int catId)
        {
            if (dicUrl.TryGetValue(refKey, out var existing))
                return existing;

            var kb = SyncConfig.Default.KanboardConfig;
            return new KanboardTask
            {
                Id = 0,
                ProjectId = kb.ProjectId,
                ColumnId = defaultColumnId,
                CategoryId = catId,
                Reference = refKey
            };
        }

        private void SaveTask(KanboardTask task, string refKey)
        {
            if (task.Id == 0)
            {
                int newId = KanboardService.CreateTask(task);
                task.Id = newId;
                dicUrl[refKey] = task;
            }
            else
            {
                KanboardService.UpdateTask(task);
            }
        }

        /// <summary>
        /// Ensure the subtask at <paramref name="index"/> exists with the correct title and status.
        /// Kanboard subtask status: 0 = todo, 1 = in-progress, 2 = done.
        /// Returns true if a create or update was performed.
        /// </summary>
        private bool EnsureSubtask(int taskId, List<KanboardSubtask> subtasks, int index, string title)
        {
            // Mirror DidaSyncService's heuristic: if the description doesn't contain "未" or "正在", it's done
            bool isDone = title != null && !title.Contains("未") && !title.Contains("正在");
            int targetStatus = isDone ? 2 : 0;

            if (index < subtasks.Count)
            {
                var sub = subtasks[index];
                bool changed = sub.Title != title || sub.Status != targetStatus;
                if (changed)
                {
                    sub.Title = title;
                    sub.Status = targetStatus;
                    KanboardService.UpdateSubtask(sub);
                }
                return changed;
            }
            else
            {
                KanboardService.CreateSubtask(new KanboardSubtask
                {
                    TaskId = taskId,
                    Title = title,
                    Status = targetStatus
                });
                return true;
            }
        }

        public bool UpdateCanvasItem(Course course, ICanvasItem item, KanboardTask task, ICanvasItemConfig config)
        {
            var modified = false;
            bool isNew = task.Id == 0;

            // Title — always set on first sync; subsequent syncs obey UpdateTitle
            if (isNew || config.UpdateTitle)
            {
                var title = CanvasStringTemplateHelper.GetTitle(course, item);
                if (task.Title == null || title.Trim() != task.Title.Trim())
                {
                    task.Title = title;
                    modified = true;
                }
            }

            // Description
            if (isNew && config.CreateContent || !isNew && config.UpdateContent)
            {
                var content = CanvasStringTemplateHelper.GetContent(item);
                if (task.Description == null || content.Trim() != (task.Description ?? "").Trim())
                {
                    task.Description = content;
                    modified = true;
                }
            }

            // Due date → Unix timestamp (0 = no due date)
            if (isNew && config.CreateDueDate || !isNew && config.UpdateDueDate)
            {
                var duetime = CanvasPreference.GetDueTime(item);
                long dateDue = duetime.HasValue
                    ? new DateTimeOffset(duetime.Value.ToUniversalTime()).ToUnixTimeSeconds()
                    : 0;
                if (task.DateDue != dateDue)
                {
                    task.DateDue = dateDue;
                    modified = true;
                }
            }

            // Importance → task color (red = important, blue = normal)
            if (isNew && config.CreateImportance || !isNew && config.UpdateImportance)
            {
                var colorId = config.SetImportance ? "red" : "blue";
                if (task.ColorId != colorId)
                {
                    task.ColorId = colorId;
                    modified = true;
                }
            }

            return modified;
        }

        private string GetCourseMessage(Course course)
            => $"处理课程 {(SyncConfig.Default.VerboseMode ? course.Name : CourseCount.ToString())} ";

        private string GetItemMessage(ICanvasItem item)
            => $"{item.GetItemName()} {(SyncConfig.Default.VerboseMode ? item.Title : ItemCount.ToString())} ";

        #endregion
    }
}
