import { LocationDescriptor } from "history";
import React from "react";
import { Route, RouteComponentProps, Switch, withRouter } from "react-router-dom";

import { IRemoteTaskQueueApi } from "./Domain/Api/RemoteTaskQueue";
import { TaskChainsTreeContainer } from "./containers/TaskChainsTreeContainer";
import { TaskDetailsPageContainer } from "./containers/TaskDetailsPageContainer";
import { TasksPageContainer } from "./containers/TasksPageContainer";

interface RemoteTaskQueueApplicationProps extends RouteComponentProps {
    remoteTaskQueueApi: IRemoteTaskQueueApi;
    customRenderer: ICustomRenderer;
    useErrorHandlingContainer: boolean;
    isSuperUser: boolean;
}

function tryGetParentLocationFromHistoryState(location: any): Nullable<LocationDescriptor> {
    if (location.state == null) {
        return null;
    }
    if (location.state.parentLocation != null) {
        const parentLocation = location.state.parentLocation;
        if (typeof parentLocation === "string") {
            return parentLocation;
        }
        if (typeof parentLocation === "object") {
            const { pathname, search } = parentLocation;
            if (typeof pathname === "string" && (search == null || typeof search === "string")) {
                return {
                    pathname: pathname,
                    search: search,
                };
            }
        }
    }
    return null;
}

export function RemoteTaskQueueApplicationInternal({ match }: RemoteTaskQueueApplicationProps): JSX.Element {
    return (
        <Switch>
            <Route
                exact
                path={`${match.url}/`}
                render={({ location }) => <TasksPageContainer searchQuery={location.search} {...location.state} />}
            />
            <Route
                exact
                path={`${match.url}/Tree`}
                render={({ location }) => (
                    <TaskChainsTreeContainer
                        searchQuery={location.search}
                        {...location.state}
                        parentLocation={tryGetParentLocationFromHistoryState(location)}
                    />
                )}
            />
            <Route
                path={`${match.url}/:id`}
                render={({ location, match: { params } }) => (
                    <TaskDetailsPageContainer
                        id={params.id || ""}
                        parentLocation={tryGetParentLocationFromHistoryState(location)}
                    />
                )}
            />
        </Switch>
    );
}

export const RemoteTaskQueueApplication = withRouter(RemoteTaskQueueApplicationInternal);
