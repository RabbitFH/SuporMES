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
    /// ����У�������
    /// </summary>
    [ApiController, Route("[controller]/[action]")]
    public class DataCheckController : ControllerBase
    {
        /// <summary>
        /// ��ȡһ��ֵ,��ֵ��ʾ֪ͨ������Ļ������Ӻ�ʱ��ֹ,���Ӧȡ���������
        /// </summary>
        protected CancellationToken Token => this.HttpContext.RequestAborted;

        /// <summary>
        /// ���Ŵ���
        /// </summary>
        private static string? DeptCode;

        /// <summary>
        /// ����������Ϣ
        /// </summary>
        private static readonly ConnectionInfo Info = new(30, "yifei.flyhigh.vip", "master", false, "sa", "7F5BB4F152A357BA");
        /// <summary>
        /// У�����
        /// </summary>
        private static readonly string Check = StringUtil.JoinLine(
            "select 1",
            "from SUPOR.SuporOEM.dbo.Invoice with (nolock)",
            "	inner join MES.flyhighdata.dbo.KHWPWHH with (nolock)",
            "		on Invoice.MCode=KHWPWHH.KHWPH collate Chinese_PRC_CI_AS",
            "where DeptCode=@dept and InvoiceCode=@invoice and WPH=@wph");

        /// <summary>
        /// ��ȡһ��ֵ,��ֵ��ʾ���Ŵ���
        /// </summary>
        private static string Dept=> DeptCode ??= GetDeptAsync(CancellationToken.None).Result;

        /// <summary>
        /// ��ȡ���Ŵ���
        /// </summary>
        /// <param name="token">������ȡ���첽������ȡ������</param>
        /// <returns>���Ŵ���</returns>
        internal static async Task<string> GetDeptAsync(CancellationToken token)
        {
            using DataSet ds = new();
            using SqlCommand command = new("select DeptCode from SUPOR.SuporOEM.dbo.WmsUser where UserID='barcode_zjfzgm'");
            await Info.GetDataSetAsync(command, ds, token);
            return ds.Tables.Count is not 1 || ds.Tables[0].Rows.Count is not 1 || ds.Tables[0].Columns.Count is not 1 ? string.Empty
                : ds.Tables[0].Rows[0][0].ToString() ?? string.Empty;
        }

        /// <summary>
        /// ���������У��
        /// </summary>
        /// <param name="ladings">�����(֧�ָ�ʽ[�����a|��Ʒ��1,�����a|��Ʒ��2,�����b|��Ʒ��2,�����b|��Ʒ��3,�����b|��Ʒ��4])</param>
        /// <returns>��ӦDTO</returns>
        [HttpGet]
        public async Task<ResponseDto> DataCheck(string ladings)
        {
            //�ָ� �����a|��Ʒ��1,�����a|��Ʒ��2,�����b|��Ʒ��2,�����b|��Ʒ��3,�����b|��Ʒ��4
            IEnumerable<(string Invoice, string Code)> list = ladings.Split(',').Select(str =>
            str.Split('|') is string[] tmp && tmp.Length is 2 ? (tmp[0], tmp[1]) : (string.Empty, string.Empty));
            //���˵���ֵ
            list = list.Where(lading => !string.IsNullOrEmpty(lading.Invoice) && !string.IsNullOrEmpty(lading.Code));

            if (!list.Any()) return ResponseDto.Error($"�����������Ʒ�Ų���Ϊ��![{ladings}]");
            if (string.IsNullOrWhiteSpace(Dept)) return ResponseDto.Error("Super ������ϵͳ��ҵ�����ȡʧ��,����ӿڷ�����!");

            //����У��(У������򷵻ش�����Ϣ)
            IEnumerable<string?> errors = (await Task.WhenAll(list.Select(async lading =>
            await DataCheckAsync(Dept, lading.Invoice, lading.Code, this.Token)
            ? null : $"�������[{lading.Invoice}]��Ʒ��[{lading.Code}]У��ʧ��!"))).Where(t => t is not null);

            if (errors.Any()) return ResponseDto.Error(string.Join(',', errors));
            //����У��ɹ�,��������
            return ResponseDto.OK("����У��ɹ�!");
        }

        /// <summary>
        /// ����У��
        /// </summary>
        /// <param name="dept">��������</param>
        /// <param name="invoice">�������</param>
        /// <param name="wph">��Ʒ��(�Լ���)</param>
        /// <param name="token">������ȡ���첽������ȡ������</param>
        /// <returns>�Ƿ�ͨ��</returns>
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
