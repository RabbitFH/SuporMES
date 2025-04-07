using BaseDto.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using NetExtensions.Utils;
using SqlDataDriven;
using System.Data;
using ConnectionInfo = SqlDataDriven.Models.ConnectionInfo;

namespace SuporMES.Controllers
{
    /// <summary>
    /// 数据校验控制器
    /// </summary>
    [ApiController, Route("[controller]/[action]")]
    public class DataCheckController : ControllerBase
    {
        /// <summary>
        /// 获取一个值,该值表示通知此请求的基础连接何时中止,因此应取消请求操作
        /// </summary>
        protected CancellationToken Token => this.HttpContext.RequestAborted;

        /// <summary>
        /// 部门代码
        /// </summary>
        private static string? DeptCode;

        /// <summary>
        /// 数据连接信息
        /// </summary>
        private static readonly ConnectionInfo Info = new(30, "yifei.flyhigh.vip", "master", false, "sa", "7F5BB4F152A357BA");
        /// <summary>
        /// 校验语句
        /// </summary>
        private static readonly string Check = StringUtil.JoinLine(
            "select 1",
            "from SUPOR.SuporOEM.dbo.Invoice with (nolock)",
            "	inner join MES.flyhighdata.dbo.KHWPWHH with (nolock)",
            "		on Invoice.MCode=KHWPWHH.KHWPH collate Chinese_PRC_CI_AS",
            "where DeptCode=@dept and InvoiceCode=@invoice and WPH=@wph");

        /// <summary>
        /// 获取一个值,该值表示部门代码
        /// </summary>
        private static string Dept=> DeptCode ??= GetDeptAsync(CancellationToken.None).Result;

        /// <summary>
        /// 获取部门代码
        /// </summary>
        /// <param name="token">可用于取消异步操作的取消令牌</param>
        /// <returns>部门代码</returns>
        internal static async Task<string> GetDeptAsync(CancellationToken token)
        {
            using DataSet ds = new();
            using SqlCommand command = new("select DeptCode from SUPOR.SuporOEM.dbo.WmsUser where UserID='barcode_zjfzgm'");
            await Info.GetDataSetAsync(command, ds, token);
            return ds.Tables.Count is not 1 || ds.Tables[0].Rows.Count is not 1 || ds.Tables[0].Columns.Count is not 1 ? string.Empty
                : ds.Tables[0].Rows[0][0].ToString() ?? string.Empty;
        }

        /// <summary>
        /// 提货单数据校验
        /// </summary>
        /// <param name="ladings">提货单(支持格式[提货单a|物品号1,提货单a|物品号2,提货单b|物品号2,提货单b|物品号3,提货单b|物品号4])</param>
        /// <returns>响应DTO</returns>
        [HttpGet]
        public async Task<ResponseDto> DataCheck(string ladings)
        {
            //分割 提货单a|物品号1,提货单a|物品号2,提货单b|物品号2,提货单b|物品号3,提货单b|物品号4
            IEnumerable<(string Invoice, string Code)> list = ladings.Split(',').Select(str =>
            str.Split('|') is string[] tmp && tmp.Length is 2 ? (tmp[0], tmp[1]) : (string.Empty, string.Empty));
            //过滤掉空值
            list = list.Where(lading => !string.IsNullOrEmpty(lading.Invoice) && !string.IsNullOrEmpty(lading.Code));

            if (!list.Any()) return ResponseDto.Error($"提货单号与物品号不能为空![{ladings}]");
            if (string.IsNullOrWhiteSpace(Dept)) return ResponseDto.Error("Super 防串货系统企业编码获取失败,请检查接口服务器!");

            //数据校验(校验错误则返回错误信息)
            IEnumerable<string?> errors = (await Task.WhenAll(list.Select(async lading =>
            await DataCheckAsync(Dept, lading.Invoice, lading.Code, this.Token)
            ? null : $"提货单号[{lading.Invoice}]物品号[{lading.Code}]校验失败!"))).Where(t => t is not null);

            if (errors.Any()) return ResponseDto.Error(string.Join(',', errors));
            //数据校验成功,返回数据
            return ResponseDto.OK("数据校验成功!");
        }

        /// <summary>
        /// 数据校验
        /// </summary>
        /// <param name="dept">部门数据</param>
        /// <param name="invoice">提货单号</param>
        /// <param name="wph">物品号(自己的)</param>
        /// <param name="token">可用于取消异步操作的取消令牌</param>
        /// <returns>是否通过</returns>
        internal static async Task<bool> DataCheckAsync(string dept,string invoice,string wph, CancellationToken token)
        {
            using DataSet ds = new();
            using SqlCommand command = new(Check);
            command.Parameters.AddValue(dept);
            command.Parameters.AddValue(invoice);
            command.Parameters.AddValue(wph);
            return (await Info.GetDataSetAsync(command, ds, token)) is 1;
        }
    }
}
