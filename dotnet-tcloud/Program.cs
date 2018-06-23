﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Newtonsoft.Json.Linq;
using XC.BlogTools.Util;
using XC.BlogTools.Util.TagProcessor;

namespace dotnet_tcloud
{
    [Command(Name = "dotnet-tcloud",
        Description = "欢迎使用 晓晨博客图片解析快速上传工具-腾讯云+社区，技术支持QQ群 4656606.Github: https://github.com/stulzq/BlogTools")]
    [HelpOption("-h|--help")]
    class Program
    {
        static async Task<int> Main(string[] args) => await CommandLineApplication.ExecuteAsync<Program>(args);

        [Argument(0, "MarkdownFilePath", "Required.Your mrkdown File Path.")]
        [Required]
        [FileExists]
        public string MarkdownFilePath { get; }

        [Option("-c|--cookie", Description = "Required.Cookie file path.")]
        [Required]
        [FileExists]
        public string CookieFilePath { get; }

        [Option("--uin", Description = "Required.")]
        [Required]
        public string Uin { get; }

        [Option("--csrf", Description = "Required.")]
        [Required]
        public string Csrf { get; }

        private async Task<int> OnExecuteAsync(CommandLineApplication app)
        {
            if (app.Options.Count == 1 && (app.Options[0].ShortName == "h" || app.Options[0].ShortName == "help"))
            {
                app.ShowHelp();
                return 0;
            }

            return await Work(MarkdownFilePath, CookieFilePath);
        }


        private async Task<int> Work(string filePath, string cookiePath)
        {
            string cookie = File.ReadAllText(cookiePath, EncodingType.GetType(cookiePath));

            var fileEncoding = EncodingType.GetType(filePath);
            var fileContent = File.ReadAllText(filePath, fileEncoding);
            var fileInfo = new FileInfo(filePath);
            var fileExtension = fileInfo.Extension;
            var fileDir = fileInfo.DirectoryName;

            var imgProc = new ImageProcessor();
            var imgList = imgProc.Process(fileContent);

            Console.WriteLine($"提取图片成功，共{imgList.Count}个.");

            var uploader = new ClientUploader("https://cloud.tencent.com");

            var replaceDic = new Dictionary<string, string>();

            foreach (var img in imgList)
            {
                string imgPhyPath = Path.Combine(fileDir, img);

                Console.WriteLine($"正在上传图片：{imgPhyPath}");
                try
                {
                    var res = await uploader.UploadAsync(imgPhyPath, $"/developer/services/ajax/image?action=UploadImage&uin={Uin}&csrfCode={Csrf}", "image",
                        new Dictionary<string, string>()
                        {
                            ["Cookie"] = cookie
                        });

                    //校验
                    var json = JObject.Parse(res);
                    if (json.Value<int>("code") != 0)
                    {
                        Console.WriteLine("Cookie或uin、csrf code 无效！");
                        return 1;
                    }

                    var httpImgPath = json["data"].Value<string>("url");

                    replaceDic.Add(img, httpImgPath);

                    Console.WriteLine("上传成功！");
                }
                catch (Exception)
                {
                    Console.WriteLine("处理失败！");
                    return 1;
                }
            }

            var newContent = imgProc.Replace(replaceDic, fileContent);
            string newFileName = filePath.Substring(0, filePath.LastIndexOf('.')) + "-imooc" + fileExtension;
            File.WriteAllText(newFileName, newContent, fileEncoding);

            Console.WriteLine($"处理完成！文件保存在：{newFileName}");

            return 0;
        }
    }
}
