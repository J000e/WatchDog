using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using WatchDog.src.Interfaces;
using WatchDog.src.Managers;
using WatchDog.src.Models;

namespace WatchDog.src {
    internal class WatchDogExceptionLogger {
        private readonly RequestDelegate _next;
        private readonly IBroadcastHelper _broadcastHelper;
        public WatchDogExceptionLogger(RequestDelegate next, IBroadcastHelper broadcastHelper) {
            _next = next;
            _broadcastHelper = broadcastHelper;
        }

        public async Task InvokeAsync(HttpContext context) {
            try {
                await _next(context);
            } catch (Exception ex) {
                await LogException(ex, WatchDog.RequestLog);
                throw;
            }
        }
        public async Task LogException(Exception ex, RequestModel requestModel) {
            Debug.WriteLine("The following exception is logged: " + ex.Message);
            var watchExceptionLog = new WatchExceptionLog {
                EncounteredAt = DateTime.Now,
                Message = ex.Message,
                StackTrace = ex.StackTrace,
                Source = ex.Source,
                TypeOf = ex.GetType().ToString(),
                Path = requestModel?.Path,
                Method = requestModel?.Method,
                QueryString = requestModel?.QueryString,
                RequestBody = requestModel?.RequestBody,
                UserName = requestModel?.UserName ?? "N/A"
            };

            //Insert
            await DynamicDBManager.InsertWatchExceptionLog(watchExceptionLog);
            await _broadcastHelper.BroadcastExLog(watchExceptionLog);
        }
    }
}
