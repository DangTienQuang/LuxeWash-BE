using Microsoft.AspNetCore.Http;
using System;
using AutoWashPro.BLL.Exceptions;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace AutoWashPro.API.Middlewares
{
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;

        public ExceptionMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                context.Response.ContentType = "application/json";

                int statusCode = 500;
                string message = "Internal server error. Please try again later.";
                string? details = null;

                string? errorCode = null;

                switch (ex)
                {
                    case BadRequestException badRequestEx:
                        statusCode = 400;
                        message = badRequestEx.Message;
                        errorCode = badRequestEx.ErrorCode;
                        break;
                    case NotFoundException notFoundEx:
                        statusCode = 404;
                        message = notFoundEx.Message;
                        errorCode = notFoundEx.ErrorCode;
                        break;
                    case ForbiddenException forbiddenEx:
                        statusCode = 403;
                        message = forbiddenEx.Message;
                        errorCode = forbiddenEx.ErrorCode;
                        break;
                    case UnauthorizedException unauthorizedEx:
                        statusCode = 401;
                        message = unauthorizedEx.Message;
                        errorCode = unauthorizedEx.ErrorCode;
                        break;
                    case ConflictException conflictEx:
                        statusCode = 409;
                        message = conflictEx.Message;
                        errorCode = conflictEx.ErrorCode;
                        break;
                    case DbUpdateConcurrencyException concurrencyEx:
                        statusCode = 409;
                        message = "Data was modified by another transaction. Please reload and try again.";
                        errorCode = "CONCURRENCY_CONFLICT";
                        break;
                    default:
                        // Fallback cho các ngoại lệ chưa định nghĩa rõ
                        if (ex.Message.Contains("not found") || ex.Message.Contains("already exists") || ex.Message.Contains("allowed") || ex.Message.Contains("must be"))
                        {
                            statusCode = 400;
                            message = ex.Message;
                        }
                        else
                        {
                            details = ex.Message; // Tuỳ chọn ẩn trên Product nhưng hiện log tạm
                        }
                        break;
                }

                context.Response.StatusCode = statusCode;

                var response = new
                {
                    statusCode = statusCode,
                    errorCode = errorCode,
                    message = message,
                    details = details
                };

                var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                var json = JsonSerializer.Serialize(response, options);
                await context.Response.WriteAsync(json);
            }
        }
    }
}