using Microsoft.OpenApi.Models;
using NetExtensions;
using NetExtensions.Utils;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Collections.Concurrent;
using System.Xml.Linq;

namespace SuporMES.Functions
{
    /// <summary>
    /// 枚举架构过滤器(Swagger):加入枚举名称及注释
    /// </summary>
    internal class EnumSchemaFilter : ISchemaFilter
    {
        /// <summary>
        /// 全局枚举注释列表缓存[Key:枚举类型;Value:枚举注释列表]
        /// </summary>
        private readonly ConcurrentDictionary<Type, IEnumerable<XElement>> EnumCache = new();

        /// <summary>
        /// 创建枚举注释列表
        /// </summary>
        /// <param name="type">枚举类型</param>
        /// <returns>枚举注释列表</returns>
        private static IEnumerable<XElement> CreateDoc(Type type)
        {
            //获取匹配的枚举注释列表,名称是F:开头(大小写不敏感)
            IEnumerable<XElement> elements =
                //公布的包含API文档的文件(xml文档文件)路径(服务不能使用相对路径)
                $"{type.Assembly.GetName().Name}.xml".CombineFullPath() is string path && File.Exists(path)
                //文件存在(获取doc=>members)
                && XDocument.Load(path) is XDocument doc && doc.Descendants("members") is IEnumerable<XElement> members
                //遍历member列表
                ? members.Descendants("member").Where(member =>
                //名称是F:开头(大小写不敏感)
                member.Attribute("name") is XAttribute element && element.Value.IndexOf("F:", StringComparison.OrdinalIgnoreCase) is 0).ToArray() : [];
            //枚举注释列表中的元素解除与父项的关联,用于释放内存
            elements.ForEach(member => member.Remove());
            return elements;
        }

        /// <summary>
        /// 架构添加枚举描述
        /// </summary>
        /// <param name="type">枚举类型</param>
        /// <param name="name">枚举名称</param>
        /// <param name="elements">枚举注释列表</param>
        /// <param name="schema">API架构对象</param>
        private static void SchemaAddEnum(Type type, string name, List<XElement> elements, OpenApiSchema schema)
        {
            //枚举值
            object value = Enum.Parse(type, name);
            //枚举描述,枚举成员名称
            string description = $"<li><i>{value:D}</i> - {value}", full = $"F:{type.FullName}.{name}";
            //描述追加注释
            elements.ForEach(member =>
            {
                if (member.Attribute("name") is XAttribute attribute && attribute.Value.EqualsIgnoreCase(full))
                {
                    description += $"({member.Value.Trim('\r', '\n').Trim()})";
                    return;
                }
            });
            //添加枚举Api信息(新的)
            //schema.Enum.Add(new OpenApiString(description));
            //修改枚举描述内容(使用Api信息会造成Example Value显示异常)
            schema.Description += $"{description}</li>";
        }

        /// <summary>
        /// 应用架构过滤
        /// </summary>
        /// <param name="schema">API架构对象</param>
        /// <param name="context">架构过滤上下文</param>
        void ISchemaFilter.Apply(OpenApiSchema schema, SchemaFilterContext context)
        {
            //枚举类型
            if (context.Type.IsEnum && schema.Enum.Count > 0 && this.EnumCache.GetOrAdd(context.Type, CreateDoc) is List<XElement> elements)
            {
                //清除枚举Api信息(原有)
                //schema.Enum.Clear();
                //追加枚举描述内容(使用Api信息会造成Example Value显示异常)
                schema.Description += "<p>Members:</p><ul>";
                //遍历枚举名称
                context.Type.GetEnumNames().ForEach(name => SchemaAddEnum(context.Type, name, elements, schema));
            }
        }
    }
}