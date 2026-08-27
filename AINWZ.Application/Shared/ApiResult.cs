namespace SpeakEase.Write.Application.Shared;

[Serializable]
public class ApiResultbase
{
    public bool Successed { get; protected set; }
    public string Message { get; set; }
    public int Status { get; set; }
}

[Serializable]
public class ApiResult<TResult> : ApiResultbase
{
    public TResult Data { get; set; }

    public ApiResult(TResult result)
    {
        Data = result;
        Successed = true;
        Status = 200;
    }

    public ApiResult(string errorMessage = "", int code = 500)
    {
        Successed = false;
        Message = errorMessage;
        Status = code;
    }

    public ApiResult()
    {
    }

    public static ApiResult Success(object result) => new(result);

    public static ApiResult Fail(string errorMessage, int errorCode) => new(errorMessage, errorCode);
}

[Serializable]
public class ApiResult : ApiResult<object>
{
    public ApiResult(object result) : base(result)
    {
    }

    public ApiResult(string errorMessage, int code = 500) : base(errorMessage, code)
    {
    }

    public ApiResult() : base()
    {
    }
}
