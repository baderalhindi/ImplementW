using PMPlatform.Api.Errors;
using PMPlatform.Application.Features.Schedule.Contracts;

namespace PMPlatform.Api.Models.Schedule;

/// <summary>A leaf's Current Forecast: when it is now expected to start and finish.</summary>
public sealed record ScheduleForecastRequest(DateOnly? ForecastStartDate, DateOnly? ForecastFinishDate)
{
    internal List<FieldError> Validate(out ScheduleForecast? forecast)
    {
        List<FieldError> errors = [];
        if (ForecastStartDate is null)
        {
            errors.Add(new FieldError("forecastStartDate", FieldError.Required));
        }

        if (ForecastFinishDate is null)
        {
            errors.Add(new FieldError("forecastFinishDate", FieldError.Required));
        }
        else if (ForecastStartDate is { } start && ForecastFinishDate < start)
        {
            errors.Add(new FieldError("forecastFinishDate", FieldError.DateBeforeStart));
        }

        forecast = errors.Count == 0 ? new ScheduleForecast(ForecastStartDate!.Value, ForecastFinishDate!.Value) : null;
        return errors;
    }
}
