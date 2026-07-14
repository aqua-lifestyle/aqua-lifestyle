import { Building2, MapPin, Presentation, Users } from "lucide-react";

import { Badge, Card, LinkButton } from "@/src/shared/ui";

export type AreaSpaceSummary = {
  address: string;
  capacity: string;
  id: number;
  interestedMembers: number;
  name: string;
  presentationsCompleted: number;
  statusText: string;
};

type AreaSpaceManagementProps = {
  areaSpace: AreaSpaceSummary | null;
};

export const AreaSpaceManagement = ({ areaSpace }: AreaSpaceManagementProps) => (
  <Card className="h-full">
    <div className="flex items-start justify-between gap-4">
      <div>
        <p className="text-sm font-semibold text-accent">Your operation</p>
        <h2 className="mt-1 text-lg font-semibold">Area Space</h2>
      </div>
      {areaSpace ? <Badge tone="info">{areaSpace.statusText}</Badge> : null}
    </div>

    {areaSpace ? (
      <>
        <div className="mt-5 flex items-center gap-3">
          <div className="rounded-xl bg-accent/10 p-3 text-accent"><Building2 className="size-6" /></div>
          <div><p className="font-semibold">{areaSpace.name}</p><p className="text-sm text-muted-foreground">Space #{areaSpace.id}</p></div>
        </div>
        <dl className="mt-5 grid gap-4 sm:grid-cols-2">
          <div className="rounded-lg bg-muted/60 p-3"><dt className="flex items-center gap-2 text-xs text-muted-foreground"><MapPin className="size-4" /> Address</dt><dd className="mt-1 text-sm font-semibold">{areaSpace.address}</dd></div>
          <div className="rounded-lg bg-muted/60 p-3"><dt className="flex items-center gap-2 text-xs text-muted-foreground"><Users className="size-4" /> Capacity</dt><dd className="mt-1 text-sm font-semibold">{areaSpace.capacity}</dd></div>
          <div className="rounded-lg bg-muted/60 p-3"><dt className="flex items-center gap-2 text-xs text-muted-foreground"><Users className="size-4" /> Interested members</dt><dd className="mt-1 text-sm font-semibold">{areaSpace.interestedMembers}</dd></div>
          <div className="rounded-lg bg-muted/60 p-3"><dt className="flex items-center gap-2 text-xs text-muted-foreground"><Presentation className="size-4" /> Presentations</dt><dd className="mt-1 text-sm font-semibold">{areaSpace.presentationsCompleted}</dd></div>
        </dl>
      </>
    ) : (
      <p className="mt-5 text-sm text-muted-foreground">No Area Space is assigned to your profile yet.</p>
    )}
    <LinkButton className="mt-5 w-full" href="/area-leader/area-space" variant="outline">Manage Area Space</LinkButton>
  </Card>
);
