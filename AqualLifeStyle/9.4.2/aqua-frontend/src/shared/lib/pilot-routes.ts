const pilotRoutes = new Set([
  "/", "/login", "/signup", "/verify-email", "/verify-email-sent", "/profile",
  "/dashboard", "/member", "/member/dashboard", "/member/programmes", "/member/programme-progress",
  "/member/invitations", "/admin", "/admin/dashboard", "/admin/programme-participations",
  "/admin/weekly-sales-reviews", "/admin/weekly-earnings",
]);
export const isPilotRoute = (path: string) => pilotRoutes.has(path.split("#")[0]) || path.startsWith("/i/");
