using System;

namespace AutoWashPro.BLL.Exceptions
{
    public class BadRequestException : Exception
    {
        public string? ErrorCode { get; }
        public BadRequestException(string message, string? errorCode = null) : base(message) { ErrorCode = errorCode; }
    }

    public class NotFoundException : Exception
    {
        public string? ErrorCode { get; }
        public NotFoundException(string message, string? errorCode = null) : base(message) { ErrorCode = errorCode; }
    }

    public class ForbiddenException : Exception
    {
        public string? ErrorCode { get; }
        public ForbiddenException(string message, string? errorCode = null) : base(message) { ErrorCode = errorCode; }
    }

    public class UnauthorizedException : Exception
    {
        public string? ErrorCode { get; }
        public UnauthorizedException(string message, string? errorCode = null) : base(message) { ErrorCode = errorCode; }
    }

    public class ConflictException : Exception
    {
        public string? ErrorCode { get; }
        public ConflictException(string message, string? errorCode = null) : base(message) { ErrorCode = errorCode; }
    }
}