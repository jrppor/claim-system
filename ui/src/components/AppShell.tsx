"use client";

import type { ReactNode } from "react";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import AppBar from "@mui/material/AppBar";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Container from "@mui/material/Container";
import Toolbar from "@mui/material/Toolbar";
import Typography from "@mui/material/Typography";
import { useQueryClient } from "@tanstack/react-query";
import { canAccess, roleLabels } from "@/lib/roles";
import { useSession } from "@/lib/useSession";

interface NavItem {
  label: string;
  href: string;
}

// Items are filtered by canAccess, so a role never sees a link to a page it cannot open.
const navItems: NavItem[] = [
  { label: "หน้าแรก", href: "/" },
  { label: "แดชบอร์ด", href: "/dashboard" },
  { label: "รายการเคลม", href: "/claims" },
  { label: "สร้างเคลม", href: "/claims/new" },
  { label: "จัดการผู้ใช้", href: "/users" },
  { label: "ฟอร์มตัวอย่าง", href: "/sample-form" },
];

export function AppShell({ children }: { children: ReactNode }) {
  const pathname = usePathname();
  const router = useRouter();
  const queryClient = useQueryClient();
  const { data: user } = useSession();

  async function signOut() {
    await fetch("/api/auth/logout", { method: "POST" });
    queryClient.clear();
    router.replace("/login");
    router.refresh();
  }

  return (
    <Box sx={{ minHeight: "100vh" }}>
      <AppBar position="static">
        <Toolbar>
          <Typography variant="h6" component="div" sx={{ flexGrow: 1 }}>
            ระบบเคลมประกันรถยนต์
          </Typography>
          {user &&
            navItems
              .filter((item) => canAccess(user.role, item.href))
              .map((item) => (
                <Button key={item.href} color="inherit" component={Link} href={item.href}>
                  {item.label}
                </Button>
              ))}
          {user && (
            <>
              <Typography variant="body2" sx={{ mx: 2 }}>
                {user.displayName} ({roleLabels[user.role]})
              </Typography>
              <Button color="inherit" onClick={signOut}>
                ออกจากระบบ
              </Button>
            </>
          )}
        </Toolbar>
      </AppBar>
      <Container maxWidth="md" sx={{ py: 4 }}>
        {children}
      </Container>
    </Box>
  );
}
