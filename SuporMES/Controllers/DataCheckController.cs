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
            "select Invoice.SingleCount",
            "from SUPOR.SuporOEM.dbo.Invoice with (nolock)",
            "	inner join MES.flyhighdata.dbo.KHWPWHH with (nolock)",
            "		on Invoice.MCode=KHWPWHH.KHWPH collate Chinese_PRC_CI_AS",
            "where DeptCode=@dept and InvoiceCode=@invoice and WPH=@wph");

        /// <summary>
        /// 获取一个值,该值表示部门代码
        /// </summary>
        private static string Dept => DeptCode ??= GetDeptAsync(CancellationToken.None).Result;

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
        /// <param name="ladings">
        /// <para>提货单支持格式如下:</para>
        /// <para>提货单a|物品号1|数量n1,提货单a|物品号2|数量n2,提货单b|物品号2|数量n3,提货单b|物品号3|数量n4,提货单b|物品号4|数量n5</para>
        /// <para>Error Message:</para>
        /// <para>1.提货单号或物品号不能为空,数量不能小于0!</para>
        /// <para>2.Super 防串货系统企业编码获取失败,请检查接口服务器!</para>
        /// <para>3.提货单号[提货单a]物品号[物品号1]不存在!</para>
        /// <para>4.Super 防串货系统:提货单号[提货单a]物品号[物品号1],数量异常!</para>
        /// <para>5.提货单号[提货单a]物品号[物品号1]数量[数量n1],当前数量超出最大提货数量!</para>
        /// </param>
        /// <returns>响应DTO</returns>
        [HttpGet]
        public async Task<ResponseDto> DataCheck(string ladings)
        {
            //分割 提货单a|物品号1|数量n1,提货单a|物品号2|数量n2,提货单b|物品号2|数量n3,提货单b|物品号3|数量n4,提货单b|物品号4|数量n5
            IEnumerable<(string Invoice, string Code, double Num)> list = ladings.Split(',').Select(str =>
            str.Split('|') is not string[] tmp ? (string.Empty, string.Empty, 0d)
            : tmp.Length is 3 ? (tmp[0], tmp[1], double.TryParse(tmp[2], out double value) ? value : 0d)
            : tmp.Length is 2 ? (tmp[0], tmp[1], 0d)
            : tmp.Length is 1 ? (tmp[0], string.Empty, 0d)
            : (string.Empty, string.Empty, 0d));

            //过滤掉空值
            //list = list.Where(lading => !string.IsNullOrEmpty(lading.Invoice) || !string.IsNullOrEmpty(lading.Code));

            if (list.Any(lading => string.IsNullOrEmpty(lading.Invoice) || string.IsNullOrEmpty(lading.Code) || lading.Num <= 0))
                return ResponseDto.Error($"提货单号或物品号不能为空,数量不能等于小于0![{ladings}]");
            if (string.IsNullOrWhiteSpace(Dept)) return ResponseDto.Error("Super 防串货系统企业编码获取失败,请检查接口服务器!");

            //数据校验(校验错误则返回错误信息)
            IEnumerable<string?> errors = (await Task.WhenAll(list.Select(async lading =>
            await DataCheckAsync(Dept, lading.Invoice, lading.Code, lading.Num, this.Token)))).Where(t => t is not null);

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
        internal static async Task<string?> DataCheckAsync(string dept, string invoice, string wph, double num, CancellationToken token)
        {
            using DataSet ds = new();
            using SqlCommand command = new(Check);
            command.Parameters.AddValue(dept);
            command.Parameters.AddValue(invoice);
            command.Parameters.AddValue(wph);
            string message = $"提货单号[{invoice}]物品号[{wph}]";
            return (await Info.GetDataSetAsync(command, ds, token)) is not 1 ? $"{message}不存在!"
                : ds.Tables.Count is not 1 || ds.Tables[0].Rows.Count is not 1 || ds.Tables[0].Columns.Count is not 1
                || !double.TryParse(ds.Tables[0].Rows[0][0].ToString(), out double single) ? $"Super 防串货系统:{message},数量异常!"
                : num > single ? $"{message}数量[{single}],当前数量超出最大提货数量!" : null;
        }
    }
}