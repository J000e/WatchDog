using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WatchDog.src.Enums;
using WatchDog.src.Helpers;
using WatchDog.src.Interfaces;
using WatchDog.src.Managers;
using WatchDog.src.Models;

namespace WatchDog.src {
    internal class WatchDog {
        public static RequestModel RequestLog;
        public static WatchDogSerializerEnum Serializer;
        private readonly RequestDelegate _next;
        private readonly RecyclableMemoryStreamManager _recyclableMemoryStreamManager;
        private readonly IBroadcastHelper _broadcastHelper;
        private readonly ILogger<WatchDog> logger;
        private readonly WatchDogOptionsModel _options;

        public WatchDog(WatchDogOptionsModel options, RequestDelegate next, IBroadcastHelper broadcastHelper, ILogger<WatchDog> logger) {
            _next = next;
            _options = options;
            _recyclableMemoryStreamManager = new RecyclableMemoryStreamManager();
            _broadcastHelper = broadcastHelper;
            this.logger = logger;
            Serializer = options.Serializer;
            WatchDogConfigModel.UserName = _options.WatchPageUsername;
            WatchDogConfigModel.Password = _options.WatchPagePassword;
            WatchDogConfigModel.Blacklist = String.IsNullOrEmpty(_options.Blacklist) ? new string[] {} : _options.Blacklist.Replace(" ", string.Empty).Split(',');
        }

        public async Task InvokeAsync(HttpContext context) {
            var requestPath = context.Request.Path.ToString().Remove(0, 1);
            if (!requestPath.Contains("WTCHDwatchpage") &&
                !requestPath.Contains("watchdog") &&
                !requestPath.Contains("WTCHDGstatics") &&
                !requestPath.Contains("favicon") &&
                !requestPath.Contains("wtchdlogger") &&
                !WatchDogConfigModel.Blacklist.Contains(requestPath, StringComparer.OrdinalIgnoreCase)) {
                //Request handling comes here
                var requestLog = await LogRequest(context);
                var responseLog = await LogResponse(context);

                var timeSpent = responseLog.FinishTime.Subtract(requestLog.StartTime);
                //Build General WatchLog, Join from requestLog and responseLog

                var watchLog = new WatchLog {
                    IpAddress = context.Connection.RemoteIpAddress.ToString(),
                    ResponseStatus = responseLog.ResponseStatus,
                    QueryString = requestLog.QueryString,
                    Method = requestLog.Method,
                    Path = requestLog.Path,
                    Host = requestLog.Host,
                    RequestBody = requestLog.RequestBody,
                    ResponseBody = responseLog.ResponseBody,
                    TimeSpent = ifNonZero(timeSpent.Hours, n => $"{n} hr") +
                        ifNonZero(timeSpent.Minutes, n => $"{n} min") +
                        ifNonZero(timeSpent.Seconds, n => $"{n} sec") +
                        ifNonZero(timeSpent.Milliseconds, n => $"{n} ms"),
                    RequestHeaders = requestLog.Headers,
                    ResponseHeaders = responseLog.Headers,
                    StartTime = requestLog.StartTime,
                    EndTime = responseLog.FinishTime,
                    UserName = requestLog.UserName
                };

                await DynamicDBManager.InsertWatchLog(watchLog);
                await _broadcastHelper.BroadcastWatchLog(watchLog);
            } else {
                await _next.Invoke(context);
            }
        }

        private string ifNonZero(int number, Func<int, string> formatCallback) {
            if (number <= 0) {
                return string.Empty;
            }
            return $"{formatCallback.Invoke(number)} ";
        }

        private async Task<RequestModel> LogRequest(HttpContext context) {
            var startTime = DateTime.Now;

            var requestBodyDto = new RequestModel() {
                RequestBody = string.Empty,
                UserName = context.User.Identity?.Name ?? "N/A",
                Host = context.Request.Host.ToString(),
                Path = context.Request.Path.ToString(),
                Method = context.Request.Method.ToString(),
                QueryString = context.Request.QueryString.ToString(),
                StartTime = startTime,
                Headers = context.Request.Headers.formatHeader(),
            };

            if (context.Request.ContentLength > 1) {
                context.Request.EnableBuffering();
                await using var requestStream = _recyclableMemoryStreamManager.GetStream();
                await context.Request.Body.CopyToAsync(requestStream);
                requestBodyDto.RequestBody = shortenBody(GeneralHelper.ReadStreamInChunks(requestStream));
                context.Request.Body.Position = 0;
            }
            RequestLog = requestBodyDto;
            return requestBodyDto;
        }

        private async Task<ResponseModel> LogResponse(HttpContext context) {
            using(var originalBodyStream = context.Response.Body) {
                try {
                    using(var originalResponseBody = _recyclableMemoryStreamManager.GetStream()) {
                        context.Response.Body = originalResponseBody;
                        await _next(context);
                        context.Response.Body.Seek(0, SeekOrigin.Begin);
                        var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync();
                        context.Response.Body.Seek(0, SeekOrigin.Begin);
                        var responseBodyDto = new ResponseModel {
                            ResponseBody = shortenBody(responseBody),
                            ResponseStatus = context.Response.StatusCode,
                            FinishTime = DateTime.Now,
                            Headers = context.Response.Headers.ContentLength > 0 ? context.Response.Headers.formatHeader() : string.Empty,
                        };
                        await originalResponseBody.CopyToAsync(originalBodyStream);
                        return responseBodyDto;
                    }
                } catch (OutOfMemoryException) {
                    return new ResponseModel {
                        ResponseBody = "OutOfMemoryException occured while trying to read response body",
                            ResponseStatus = context.Response.StatusCode,
                            FinishTime = DateTime.Now,
                            Headers = context.Response.Headers.ContentLength > 0 ? context.Response.Headers.formatHeader() : string.Empty,
                    };
                } finally {
                    context.Response.Body = originalBodyStream;
                }
            }
        }

        private string shortenBody(string body) {
            try {
                JObject jsonObj = JObject.Parse(body);
                logger.LogDebug($"Body is looking like a json: {body}");

                foreach (JProperty property in jsonObj.DescendantsAndSelf().OfType<JProperty>()) {
                    if (property.Value is JValue jValue && jValue.Type == JTokenType.String) {
                        string originalValue = jValue.Value.ToString();
                        if (originalValue.Length > 100) {
                            jValue.Value = $"{originalValue.Substring(0, 97)}...";
                        }
                    }
                }

                return jsonObj.ToString(Formatting.Indented);
            } catch (JsonReaderException) {
                logger.LogDebug($"Body is not a json: {body}");

                return body.Length > 200 ? body.Substring(0, 200) : body;
            }
        }
    }
}
