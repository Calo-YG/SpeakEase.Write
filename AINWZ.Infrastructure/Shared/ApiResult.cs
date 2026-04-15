namespace AINWZ.Infrastructure.Shared
{
    [Serializable]
    public class ApiResultbase
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Successed { get; protected set; }

        /// <summary>
        /// 错误信息
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 状态码
        /// </summary>
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

        public static ApiResult Success(object result)
        {
            return new ApiResult(result);
        }

        public static ApiResult Fail(string errorMessage, int errorCode)
        {
            return new(errorMessage, errorCode);
        }
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
}
