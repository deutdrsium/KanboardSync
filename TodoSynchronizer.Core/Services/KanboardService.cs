using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using TodoSynchronizer.Core.Models;
using TodoSynchronizer.Core.Models.KanboardModels;

namespace TodoSynchronizer.Core.Services
{
    public static class KanboardService
    {
        private static HttpClient Client { get; set; }
        private static string Endpoint { get; set; }

        public static void Init(string baseUrl, string apiToken)
        {
            Endpoint = baseUrl.TrimEnd('/') + "/jsonrpc.php";
            Client = new HttpClient();
            var creds = Convert.ToBase64String(Encoding.ASCII.GetBytes($"jsonrpc:{apiToken}"));
            Client.DefaultRequestHeaders.Add("Authorization", $"Basic {creds}");
        }

        private static T Call<T>(string method, object parameters)
        {
            var body = JsonConvert.SerializeObject(new
            {
                jsonrpc = "2.0",
                method,
                id = 1,
                @params = parameters
            });

            var httpContent = new StringContent(body, Encoding.UTF8, "application/json");
            var postTask = Client.PostAsync(Endpoint, httpContent);
            postTask.Wait();
            var response = postTask.GetAwaiter().GetResult();
            var text = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"HTTP {(int)response.StatusCode}: {text}");

            var wrapper = JsonConvert.DeserializeObject<RpcResponse<JToken>>(text);
            if (wrapper.Error != null)
                throw new Exception($"Kanboard RPC [{wrapper.Error.Code}]: {wrapper.Error.Message}");

            // Kanboard returns boolean false instead of an error when an operation fails
            if (wrapper.Result is JValue jv && jv.Type == JTokenType.Boolean && !(bool)jv.Value)
                throw new Exception($"Kanboard method '{method}' returned false (operation failed)");

            if (wrapper.Result == null)
                return default;

            return wrapper.Result.ToObject<T>();
        }

        public static CommonResult TestConnection()
        {
            try
            {
                Call<string>("getVersion", new { });
                return new CommonResult(true, "连接成功");
            }
            catch (Exception ex)
            {
                return new CommonResult(false, ex.Message);
            }
        }

        /// <summary>Get all active (open) tasks in a project.</summary>
        public static List<KanboardTask> GetAllActiveTasks(int projectId)
            => Call<List<KanboardTask>>("getAllTasks", new { project_id = projectId, status_id = 1 })
               ?? new List<KanboardTask>();

        /// <summary>Get closed tasks in a project.</summary>
        public static List<KanboardTask> GetClosedTasks(int projectId)
        {
            try
            {
                return Call<List<KanboardTask>>("getAllTasks", new { project_id = projectId, status_id = 0 })
                       ?? new List<KanboardTask>();
            }
            catch
            {
                return new List<KanboardTask>();
            }
        }

        public static int CreateTask(KanboardTask t)
            => Call<int>("createTask", new
            {
                title = t.Title,
                project_id = t.ProjectId,
                description = t.Description,
                // date_due must be an integer Unix timestamp, NOT a string
                date_due = t.DateDue > 0 ? (object)t.DateDue : null,
                column_id = t.ColumnId > 0 ? (object)t.ColumnId : null,
                category_id = t.CategoryId > 0 ? (object)t.CategoryId : null,
                color_id = string.IsNullOrEmpty(t.ColorId) ? null : t.ColorId,
                reference = t.Reference
            });

        public static bool UpdateTask(KanboardTask t)
            => Call<bool>("updateTask", new
            {
                id = t.Id,
                title = t.Title,
                description = t.Description,
                // date_due must be an integer Unix timestamp, NOT a string
                date_due = t.DateDue > 0 ? (object)t.DateDue : null,
                color_id = string.IsNullOrEmpty(t.ColorId) ? null : t.ColorId,
                reference = t.Reference
            });

        public static List<KanboardCategory> GetAllCategories(int projectId)
            => Call<List<KanboardCategory>>("getAllCategories", new { project_id = projectId })
               ?? new List<KanboardCategory>();

        public static int CreateCategory(int projectId, string name)
            => Call<int>("createCategory", new { project_id = projectId, name });

        /// <summary>Returns the id of the first column of a project, or 0 if unavailable.</summary>
        public static int GetFirstColumnId(int projectId)
        {
            try
            {
                var cols = Call<List<ColumnInfo>>("getColumnsByProjectId", new { project_id = projectId });
                return cols?.Count > 0 ? cols[0].Id : 0;
            }
            catch
            {
                return 0;
            }
        }

        public static List<KanboardSubtask> GetSubtasksByTaskId(int taskId)
            => Call<List<KanboardSubtask>>("getAllSubtasks", new { task_id = taskId })
               ?? new List<KanboardSubtask>();

        public static int CreateSubtask(KanboardSubtask s)
            => Call<int>("createSubtask", new { task_id = s.TaskId, title = s.Title, status = s.Status });

        public static bool UpdateSubtask(KanboardSubtask s)
            => Call<bool>("updateSubtask", new { id = s.Id, task_id = s.TaskId, title = s.Title, status = s.Status });

        #region Private helpers
        private class RpcResponse<T>
        {
            [JsonProperty("result")] public T Result { get; set; }
            [JsonProperty("error")] public RpcError Error { get; set; }
        }

        private class RpcError
        {
            [JsonProperty("code")] public int Code { get; set; }
            [JsonProperty("message")] public string Message { get; set; }
        }

        private class ColumnInfo
        {
            [JsonProperty("id")] public int Id { get; set; }
        }
        #endregion
    }
}
