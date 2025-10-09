using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Warframe_Progress_Tracker.Model;

namespace Warframe_Progress_Tracker.View.TemplateSelectors
{
    public class CodexTemplateSelector : DataTemplateSelector
    {
        public DataTemplate ItemTemplate {  get; set; }
        public DataTemplate NodeTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            return item switch
            {
                Item => ItemTemplate,
                Node => NodeTemplate,
                _ => base.SelectTemplate(item, container)
            };
        }
    }
}
