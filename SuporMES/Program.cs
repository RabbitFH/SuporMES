using NetExtensions;
using NetExtensions.Utils;
using SuporMES.Functions;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

builder.Host.UseWindowsService();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
//添加Swagger消息,用于Swagger UI 所显示的开放文档内容
builder.Services.AddSwaggerGen(options =>
{
    //注入SuporMES API描述文件(服务不能使用相对路径)
    options.IncludeXmlComments($"{nameof(SuporMES)}.xml".CombineFullPath(), true);
    //注入领域实例API描述文件(服务不能使用相对路径)
    Directory.EnumerateFiles(AppContext.BaseDirectory, $"*Dto.xml").ForEach(xml => options.IncludeXmlComments(xml, true));
    //定义JWT架构枚举描述
    options.SchemaFilter<EnumSchemaFilter>();
});

WebApplication app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthorization();

app.MapControllers();

app.Run();