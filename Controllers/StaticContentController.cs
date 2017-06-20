﻿using KenticoCloud.Delivery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using NavigationMenusMvc.Helpers;
using NavigationMenusMvc.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NavigationMenusMvc.Controllers
{
    public class StaticContentController : BaseController
    {
        private const string DEFAULT_VIEW = "Default";

        private readonly INavigationProvider _navigationProvider;
        private readonly IContentResolver _contentResolver;

        public StaticContentController(IDeliveryClient deliveryClient, IMemoryCache memoryCache, INavigationProvider navigationProvider, IContentResolver contentResolver) : base(deliveryClient, memoryCache)
        {
            _navigationProvider = navigationProvider ?? throw new ArgumentNullException(nameof(navigationProvider));
            _contentResolver = contentResolver ?? throw new ArgumentNullException(nameof(contentResolver));
        }

        public async Task<ActionResult> Index(string urlPath)
        {
            IContentResolverResults results;

            try
            {
                results = await _contentResolver.ResolveRelativeUrlPath(urlPath);
            }
            catch (Exception ex)
            {
                return new ContentResult
                {
                    Content = $"There was an error while resolving the URL. Check if your URL was correct and try again. Details: {ex.Message}",
                    StatusCode = 500
                };
            }

            if (results != null)
            {
                if (results.Found)
                {
                    if (results.ContentItemCodenames != null && results.ContentItemCodenames.Any())
                    {
                        return await RenderViewAsync(results.ContentItemCodenames, results.ViewName);
                    }
                    else if (!string.IsNullOrEmpty(results.RedirectUrl))
                    {
                        return LocalRedirectPermanent($"/{results.RedirectUrl}");
                    }
                }
                else if (!string.IsNullOrEmpty(results.RedirectUrl))
                {
                    return RedirectPermanent(results.RedirectUrl);
                }
            }
            
            return NotFound();
        }

        private async Task<ViewResult> RenderViewAsync(IEnumerable<string> codenames, string viewName)
        {
            var navigationTask = _navigationProvider.GetOrCreateCachedNavigationAsync();

            // Separate request for page body content. Separate caching, separate depth of modular content.
            var pageBodyTask = _deliveryClient.GetItemsAsync<object>(new InFilter("system.codename", codenames.ToArray()));

            await Task.WhenAll(navigationTask, pageBodyTask);

            var pageViewModel = new PageViewModel
            {
                Navigation = navigationTask.Result,
                Body = pageBodyTask.Result.Items
            };

            return View((string.IsNullOrEmpty(viewName) ? DEFAULT_VIEW : viewName), pageViewModel);
        }
    }
}