import {DI} from "aurelia";
import {IOmniSharpApiClient, OmniSharpApiClient} from "./api";
import {Semaphore} from "@common";

export interface IOmniSharpService extends IOmniSharpApiClient {
}

export const IOmniSharpService = DI.createInterface<IOmniSharpService>();

export class OmniSharpService extends OmniSharpApiClient implements IOmniSharpService {
    // We want to limit omnisharp calls to only a specific number at a at a time
    // OmniSharp calls can get pretty chatty and can end up clogging up
    // the browser's 6 network connection limit for a while. This results in
    // poor app responsiveness (ex, adding many new scripts quickly).
    private static semaphore = new Semaphore(3);

    protected override async makeFetchCall(fetchCall: () => Promise<Response>): Promise<Response> {
        await OmniSharpService.semaphore.acquire();

        try {
            return await fetchCall();
        } catch(ex) {
            // Catch abort errors
            if (ex instanceof Error && ex.name?.startsWith("AbortError")) {
                return new Response(undefined);
            }
            throw ex;
        } finally {
            OmniSharpService.semaphore.release();
        }
    }
}

