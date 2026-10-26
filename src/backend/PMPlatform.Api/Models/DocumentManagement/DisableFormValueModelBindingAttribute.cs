using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace PMPlatform.Api.Models.DocumentManagement;

/// <summary>
/// An upload action reads its own form (<see cref="DocumentUploadForm"/>) under the upload policy's size limit. Without this,
/// MVC's form value providers read the body first, with the server's default limits, before the action can set its own.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class DisableFormValueModelBindingAttribute : Attribute, IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        IList<IValueProviderFactory> factories = context.ValueProviderFactories;
        foreach (IValueProviderFactory factory in factories.Where(f => f is FormValueProviderFactory or FormFileValueProviderFactory or JQueryFormValueProviderFactory).ToList())
        {
            factories.Remove(factory);
        }
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}
