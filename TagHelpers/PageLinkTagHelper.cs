using BulkyBook.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BulkyBook.TagHelpers
{
    [HtmlTargetElement("div", Attributes = "page-model")]
    public class PageLinkTagHelper : TagHelper
    {
        //Customize 1 taghelp thì cần phải có access tới HttpRequest và HttpResponse
        //nên sẽ có 1 thuộc tính ViewContext vì lớp ViewContext sẽ chứa 2 thằng trên
        [ViewContext] //Để compiler biết thằng này hứng ViewContext khi tiêm vào
        [HtmlAttributeNotBound] //không phải là thuộc tính của 1 thẻ html
        public ViewContext ViewContext { get; set; }

        //root model for tag helper
        public PagingInfo PageModel { get; set; }


        //Những thằng dưới sẽ bị triggered khi người dùng click button hay con số
        public string PageAction { get; set; }
        public bool PageClassesEnabled { get; set; }
        public string PageClass { get; set; }
        public string PageClassNormal { get; set; }
        public string PageClassSelected { get; set; }

        //Để hoạt động được như ý mình thì phải override lại hàm void Process(TagHelperContext context, TagHelperOutput output)
        //Không thì nó sẽ tự gọi hàm Process của class cha TagHelper
        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            //base.Process(context, output);
            //Khai báo 1 đối tượng TagBuilder với tham số là tên của 1 tag html
            //để xây dựng cho tag mà mình muốn
            TagBuilder result = new TagBuilder("div");
            
            //Giả sử có 15 items trong list
            //với điều kiện là 5 items/trang
            //vậy nghĩa là sẽ chia ra 3 trang
            //với trang 1 bắt đầu từ 0 -> 4,
            //trang 2 là 5 -> 9
            //trang 3 là 10 -> 14
            for (int i = 1; i <= PageModel.TotalPage; i++)
            {
                TagBuilder tag = new TagBuilder("a");
                string url = PageModel.UrlParam.Replace(":", i.ToString());
                tag.Attributes["href"] = url;
                if (PageClassesEnabled)
                {
                    tag.AddCssClass(PageClass);
                    tag.AddCssClass(i == PageModel.CurrentPage ? PageClassSelected : PageClassNormal);
                }
                tag.InnerHtml.Append(i.ToString());
                result.InnerHtml.AppendHtml(tag);
            }

            output.Content.AppendHtml(result.InnerHtml);
        }
    }
}
