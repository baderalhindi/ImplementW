import { ApiError } from '@/shared/api/httpClient.ts';

import { withRequestedStart } from './activityForm.ts';
import { scheduleApi } from './api/scheduleApi.ts';
import { type ScheduleActivityDetail } from './api/types.ts';
import { type DateRange, type DateTrack } from './dependencyRules.ts';

/**
 * Saves dates an activity was moved to on SCR-045. The plan moves by its requested start, the backend recalculating
 * the planned dates of the activity and of everything after it (D-3); the forecast is set as it was dropped (D-5).
 * The activity is read again for the ETag its command sends; if it changed since the chart was drawn, nothing is sent
 * and the move is refused as stale, as a 412 would be.
 */
export async function saveMove(
  shown: ScheduleActivityDetail,
  dates: DateRange,
  track: DateTrack,
): Promise<ScheduleActivityDetail> {
  const current = await scheduleApi.activity(shown.id);
  if (current.data.updatedAt !== shown.updatedAt) {
    throw new ApiError(412, 'PRECONDITION_FAILED', [], null);
  }
  const saved =
    track === 'plan'
      ? await scheduleApi.updateActivity(
          shown.id,
          withRequestedStart(current.data, dates.start),
          current.etag,
        )
      : await scheduleApi.reforecast(
          shown.id,
          { forecastStartDate: dates.start, forecastFinishDate: dates.finish },
          current.etag,
        );
  return saved.data;
}
