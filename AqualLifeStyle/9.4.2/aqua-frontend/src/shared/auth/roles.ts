export type RoleHome = {
  href: string;
  label: string;
};

const normalizeRole = (role: string | null | undefined) =>
  role?.replace(/[\s_-]/g, "").toLowerCase() ?? "";

export const isSystemAdmin = (role: string | null | undefined) =>
  ["admin", "systemadmin"].includes(normalizeRole(role));

export const isAreaLeader = (role: string | null | undefined) =>
  normalizeRole(role) === "arealeader";

export const isFacilitator = (role: string | null | undefined) =>
  normalizeRole(role) === "facilitator";

export const getRoleHome = (role: string | null | undefined): RoleHome => {
  if (isSystemAdmin(role)) {
    return { href: "/admin/dashboard", label: "Go to admin dashboard" };
  }

  if (isAreaLeader(role)) {
    return {
      href: "/area-leader/dashboard",
      label: "Go to Area Leader dashboard",
    };
  }

  if (isFacilitator(role)) {
    return {
      href: "/facilitator/dashboard",
      label: "Go to Facilitator dashboard",
    };
  }

  return { href: "/dashboard", label: "Go to customer dashboard" };
};

const isInternalPath = (path: string | null): path is string =>
  Boolean(path?.startsWith("/") && !path.startsWith("//") && !path.includes("\\"));

const isWithinRoute = (path: string, route: string) =>
  path === route || path.startsWith(`${route}/`) || path.startsWith(`${route}?`);

export const getLoginDestination = (
  role: string | null | undefined,
  redirect: string | null,
) => {
  const home = getRoleHome(role);
  const safeRedirect = isInternalPath(redirect) ? redirect : null;

  if (isSystemAdmin(role)) {
    return safeRedirect && isWithinRoute(safeRedirect, "/admin")
      ? safeRedirect
      : home.href;
  }

  if (isAreaLeader(role)) {
    return safeRedirect && isWithinRoute(safeRedirect, "/area-leader")
      ? safeRedirect
      : home.href;
  }

  if (isFacilitator(role)) {
    return safeRedirect && isWithinRoute(safeRedirect, "/facilitator")
      ? safeRedirect
      : home.href;
  }

  const targetsPrivilegedArea = ["/admin", "/area-leader", "/facilitator"].some(
    (route) => safeRedirect && isWithinRoute(safeRedirect, route),
  );

  return safeRedirect && !targetsPrivilegedArea ? safeRedirect : home.href;
};
