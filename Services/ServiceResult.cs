namespace WebApplication2.Services
{
    // Every service method returns a ServiceResult instead of throwing exceptions
    // for normal business-rule failures (e.g. "table already booked").
    // Pages check .Success and show .Error to the user.
    public class ServiceResult
    {
        public bool Success { get; protected set; }
        public string? Error { get; protected set; }

        public static ServiceResult Ok() => new() { Success = true };
        public static ServiceResult Fail(string error) => new() { Success = false, Error = error };
    }

    public class ServiceResult<T> : ServiceResult
    {
        public T? Value { get; private set; }

        public static ServiceResult<T> Ok(T value) => new() { Success = true, Value = value };
        public new static ServiceResult<T> Fail(string error) => new() { Success = false, Error = error };
    }
}
