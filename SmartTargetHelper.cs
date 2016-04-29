using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Configuration;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Mvc;
using System.Web.Mvc.Html;
using System.Xml;
using System.Xml.Serialization;
using DD4T.ContentModel;
using DD4T.ContentModel.Contracts.Providers;
using DD4T.ContentModel.Factories;
using DD4T.Factories;
using DD4T.Mvc.Html;
using DD4T.Utils;
using Tridion.ContentDelivery.AmbientData;
using Tridion.ContentDelivery.DynamicContent;
using Tridion.ContentDelivery.DynamicContent.Query;
using Tridion.ContentDelivery.Meta;
using Tridion.SmartTarget.Query;
using Tridion.SmartTarget.Query.Builder;
using Tridion.SmartTarget.Utils;
using SM = Tridion.SmartTarget; 
using log4net;


namespace Yourprojectname.Tridion.WebApplication.Helpers
{
    /// <summary>
    /// Helper used to pull SmartTarget content
    /// </summary>
    public static class SmartTargetHelper
    {
        private static readonly ILog LOG = LogManager.GetLogger(typeof(SmartTargetHelper));

        /// <summary>
        /// Renders each Smart Target region into a list which can be traversed by the caller.
        /// </summary>
        /// <param name="helper">The helper to extend</param>
        /// <param name="regionName">the name of the region to render</param>
        /// <param name="viewName">the name of the component view</param>
        /// <param name="maxItems">the number to limit on</param>
        /// <param name="startIndex">the item index at which to start rendering</param>
        /// <returns>a list of rendered items for a given region across all promotions</returns>
        /// 
        public static List<MvcHtmlString> RenderSmartTargetRegionItemsUsingView(this HtmlHelper helper, string regionName, string viewName, int maxItems = 0, int startIndex = 0)
        {

            string publicationId = ConfigurationManager.AppSettings["PublicationId"];
            LOG.Info(string.Format("Calling  RenderSmartTargetRegionItemsUsingView"));
            // First query Fredhopper for the targeted component IDs

            ClaimStore claimStore = AmbientDataContext.CurrentClaimStore;

            string query = AmbientDataHelper.GetTriggers(claimStore);
            
            var queryBuilder = new QueryBuilder();
            
            queryBuilder.Parse(query);

            if (maxItems > 0)
            {
                LOG.Info(string.Format("Maxitems ", maxItems));
                queryBuilder.MaxItems = maxItems;
            }
            LOG.Info("maxItems Value: " + maxItems.ToString());

            queryBuilder.StartIndex = startIndex;

            //Add Publication Info
            var pubIdUri = new SM.Utils.TcmUri(publicationId);
            SM.Query.Builder.PublicationCriteria pubCriteria  = new SM.Query.Builder.PublicationCriteria(pubIdUri);
            queryBuilder.AddCriteria(pubCriteria);
            

            //Add Region Info
            RegionCriteria regionCriteria = new RegionCriteria(regionName);
            queryBuilder.AddCriteria(regionCriteria);
       
            ResultSet fredHopperResultset = queryBuilder.Execute();

            List<string> componentIds = new List<string>();
            foreach (Promotion p in fredHopperResultset.Promotions)
            {
                LOG.Info("Promotion ID " + p.PromotionId.ToString());
                LOG.Info("Promotion ID " + p.Items.Count().ToString());
                foreach (Item i in p.Items)
                {
                    LOG.Info("Component ID " + i.ComponentUri.ToString());
                    LOG.Info("Template ID " + i.TemplateUri.ToString());

                    componentIds.Add(i.ComponentUriAsString + "|" + i.TemplateUriAsString);

                }
            }

            // Next, query the standard Tridion Broker to get the components out.
            // This is because we should use the master source of published content.
            // Using the CP source that has been published to Fredhopper (see API  or service response).
            // is not recommended, so we use the master source of published content, i.e. the Tridion Broker.
        
            var renderedRegionItemsList = new List<MvcHtmlString>();
            
            foreach (string s in componentIds)
            {
                string[] compPresIds = s.Split(new char[] { '|' });
                string compId = compPresIds[0], templateId = compPresIds[1];

                // We now have the Model (i.e. the Component), but we need to call the View, which is the title of the CT.
                // The issue is that the Broker API does not expose (nor store) the title of CTs.  So the only way to get this
                // is to grab it from DD4T's rendered Component Presentation XML.
                IComponent comp = null;
                ComponentFactory cf = new ComponentFactory();
                cf.TryGetComponent(compId, out comp, templateId);
                
                try
                {
                    var renderedCp = helper.Partial(viewName, comp);
                    renderedRegionItemsList.Add(renderedCp);
                }
                catch (Exception ex)
                {
                    LOG.Info("ex : " + ex.Message);
                }
            }
            return renderedRegionItemsList;
        }

        /// <summary>
        /// renders the smart target region
        /// </summary>
        /// <param name="helper">the html helper to extend</param>
        /// <param name="regionName">the name of the region</param>
        /// <param name="componentViewName">the name of the component view</param>
        /// <param name="maxItems">the maximum number of results to return</param>
        /// <param name="startIndex">which returned item to start the result set on</param>
        /// <returns>returns a concatenated string of all Smart-Targeted component presentations</returns>
        public static MvcHtmlString RenderSmartTargetRegionUsingView(this HtmlHelper helper, string regionName, string componentViewName, int maxItems = 0, int startIndex = 0)
        {
            try
            {
                LOG.Info("Fetching Components");
                List<MvcHtmlString> renderedRegionItems = RenderSmartTargetRegionItemsUsingView(helper, regionName, componentViewName, maxItems, startIndex);

                LOG.Info("Rendering Component");
                var sb = new StringBuilder();
                foreach (var item in renderedRegionItems)
                {
                    LOG.Info("ToHtmlString : " + item.ToHtmlString());
                    sb.Append(item.ToHtmlString());
                }
                return MvcHtmlString.Create(sb.ToString());
            }
            catch (Exception ex)
            {
                LOG.Info("ex_RenderSmartTargetRegionUsingView : " + ex.Message);
                return null;
            }
        }

        public static MvcHtmlString Test(this HtmlHelper helper, string regionName)
        {
            return MvcHtmlString.Create("From Helper");
        }
    }
}