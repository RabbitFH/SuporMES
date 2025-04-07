using NetExtensions;
using NetExtensions.Utils;
using SuporMES.Functions;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

builder.Host.UseWindowsService();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
//���Swagger��Ϣ,����Swagger UI ����ʾ�Ŀ����ĵ�����
builder.Services.AddSwaggerGen(options =>
{
    //ע��SuporMES API�����ļ�(������ʹ�����·��)
    options.IncludeXmlComments($"{nameof(SuporMES)}.xml".CombineFullPath(), true);
    //ע������ʵ��API�����ļ�(������ʹ�����·��)
    Directory.EnumerateFiles(AppContext.BaseDirectory, $"*Dto.xml").ForEach(xml => options.IncludeXmlComments(xml, true));
    //����JWT�ܹ�ö������
    options.SchemaFilter<EnumSchemaFilter>();
});

WebApplication app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthorization();

app.MapControllers();

app.Run();