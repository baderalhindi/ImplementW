import { apiRequest } from '@/shared/api/httpClient.ts';
import { MAX_PAGE_SIZE } from '@/shared/api/paging.ts';

import {
  type MyUpdateRequest,
  type MyUpdateRequestPage,
  type UpdateRequestBatchDetail,
  type UpdateRequestBatchPage,
  type UpdateRequestBatchRequest,
} from './monthlyUpdateTypes.ts';

export const myUpdateRequestsApi = {
  /** Everything waiting on the signed-in person, whoever they are, page after page; the counts are the API's. */
  listAll: async (signal?: AbortSignal): Promise<MyUpdateRequestPage> => {
    const items: MyUpdateRequest[] = [];
    for (let page = 1; ; page += 1) {
      const { data } = await apiRequest<MyUpdateRequestPage>('/my-update-requests', {
        query: { page, pageSize: MAX_PAGE_SIZE },
        signal,
      });
      items.push(...data.items);
      if (data.items.length === 0 || page * MAX_PAGE_SIZE >= data.totalCount) {
        return { ...data, items, page: 1 };
      }
    }
  },
  /** Records the first opening of a project's form: the batch page's "opened". */
  markOpened: (requestId: string) =>
    apiRequest<undefined>(`/my-update-requests/${requestId}/mark-opened`, { method: 'POST' }),
};

export const updateRequestBatchesApi = {
  list: async (page: number, pageSize: number, signal?: AbortSignal) =>
    (
      await apiRequest<UpdateRequestBatchPage>('/update-request-batches', {
        query: { page, pageSize },
        signal,
      })
    ).data,
  get: async (id: string, signal?: AbortSignal) =>
    (await apiRequest<UpdateRequestBatchDetail>(`/update-request-batches/${id}`, { signal })).data,
  /** All or nothing: every project that cannot be sent to is named in the 422. */
  send: async (request: UpdateRequestBatchRequest) =>
    (
      await apiRequest<UpdateRequestBatchDetail>('/update-request-batches', {
        method: 'POST',
        body: request,
      })
    ).data,
  remindLate: (id: string) =>
    apiRequest<UpdateRequestBatchDetail>(`/update-request-batches/${id}/remind-late`, {
      method: 'POST',
    }),
  /** Cancels what is still open and stops the series; answers already submitted stay with their reviewer. */
  withdraw: (id: string, reason: string) =>
    apiRequest<UpdateRequestBatchDetail>(`/update-request-batches/${id}/withdraw`, {
      method: 'POST',
      body: { reason },
    }),
};
