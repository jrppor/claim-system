"use client";

import { createTheme } from "@mui/material/styles";
import { Noto_Sans_Thai } from "next/font/google";

const notoSansThai = Noto_Sans_Thai({
  subsets: ["thai", "latin"],
  weight: ["400", "500", "700"],
  display: "swap",
});

export const theme = createTheme({
  cssVariables: true,
  palette: {
    primary: { main: "#1565c0" },
    secondary: { main: "#00897b" },
    background: { default: "#f5f7fa" },
  },
  typography: {
    fontFamily: `${notoSansThai.style.fontFamily}, "Segoe UI", sans-serif`,
  },
  shape: { borderRadius: 8 },
});
