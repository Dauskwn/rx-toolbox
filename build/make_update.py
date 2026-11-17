#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""生成自动更新用的静态目录：dist/update/{exe, version.json, _headers}

用法：
    python build/make_update.py --base https://rx-toolbox.pages.dev --notes "修正 SCP-610"
    python build/make_update.py --base https://rx-toolbox.pages.dev --version 1.6.1

把 dist/update/ 整个文件夹拖到 Cloudflare Pages 就完成发布。
"""
from __future__ import print_function

import argparse
import hashlib
import io
import json
import os
import shutil
import sys

try:
    from urllib.parse import quote
except ImportError:                                    # python 2
    from urllib import quote

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DIST = os.path.join(ROOT, "dist")


def sha256_of(path):
    h = hashlib.sha256()
    with io.open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1024 * 256), b""):
            h.update(chunk)
    return h.hexdigest()


def read_version():
    """优先读 VERSION 文件，其次解析 program.cs 里的 AssemblyVersion"""
    p = os.path.join(ROOT, "VERSION")
    if os.path.isfile(p):
        v = io.open(p, encoding="utf-8").read().strip()
        if v:
            return v
    p = os.path.join(ROOT, "build", "program.cs")
    if os.path.isfile(p):
        for line in io.open(p, encoding="utf-8"):
            if "AssemblyVersion" in line and '"' in line:
                return line.split('"')[1]
    return "0.0.0"


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--base", default="", help="站点根地址，例如 https://rx-toolbox.pages.dev")
    ap.add_argument("--version", default="", help="版本号，默认读 VERSION 文件")
    ap.add_argument("--notes", default="", help="更新说明，换行用 \\n")
    ap.add_argument("--exe", default="", help="指定 exe 路径，默认取 dist 下最新的 *.exe（默认 Rx工具箱.exe）")
    ap.add_argument("--fallback", default="", help="备用下载地址（QQ群/网盘），可留空")
    ap.add_argument("--out", default=os.path.join(DIST, "update"), help="输出目录")
    args = ap.parse_args()

    exe = args.exe
    if not exe:
        cands = [os.path.join(DIST, f) for f in os.listdir(DIST)
                 if f.lower().endswith(".exe") and "dev" not in f.lower()] if os.path.isdir(DIST) else []
        if not cands:
            print("找不到 exe，请用 --exe 指定", file=sys.stderr)
            return 1
        exe = max(cands, key=os.path.getmtime)

    if not os.path.isfile(exe):
        print("exe 不存在：" + exe, file=sys.stderr)
        return 1

    version = args.version or read_version()
    name = os.path.basename(exe)
    if args.base:
        url = args.base.rstrip("/") + "/" + quote(name)
    else:
        url = name
    notes = args.notes.replace("\\n", "\n")

    out = args.out
    if not os.path.isdir(out):
        os.makedirs(out)
    shutil.copy2(exe, os.path.join(out, name))

    # 数据文件：程序更新时必须一起换，否则升级上来是空表
    data_src = os.path.join(ROOT, "phrases.json")
    data_url = ""
    data_sha = ""
    if os.path.isfile(data_src):
        shutil.copy2(data_src, os.path.join(out, "phrases.json"))
        data_url = (args.base.rstrip("/") + "/" + quote("phrases.json")) if args.base else "phrases.json"
        data_sha = sha256_of(data_src)
    else:
        print("警告：没找到 phrases.json，这次更新不带数据文件")

    meta = {
        "version": version,
        "url": url,
        "fallback": args.fallback,
        "sha256": sha256_of(exe),
        "notes": notes,
        "minVersion": "1.5.0",
        "dataUrl": data_url,
        "dataSha256": data_sha,
    }
    with io.open(os.path.join(out, "version.json"), "w", encoding="utf-8", newline="\n") as f:
        json.dump(meta, f, ensure_ascii=False, indent=2)
        f.write("\n")

    # Pages 是「整站替换」：不把 _worker.js 带上，/api/* 就会 404，
    # 表现就是开发者工具里上传失败。每次部署都必须一起发。
    worker_src = os.path.join(ROOT, "build", "pages", "_worker.js")
    if os.path.isfile(worker_src):
        shutil.copy2(worker_src, os.path.join(out, "_worker.js"))
    else:
        print("警告：没找到 build/pages/_worker.js，这次部署会让 /api/* 失效")

    headers = (
        "/version.json\n"
        "  Cache-Control: no-store\n"
        "\n"
        "/phrases.json\n"
        "  Cache-Control: no-store\n"
        "\n"
        "/*.exe\n"
        "  Cache-Control: public, max-age=3600\n"
        "  Content-Disposition: attachment\n"
    )
    with io.open(os.path.join(out, "_headers"), "w", encoding="utf-8", newline="\n") as f:
        f.write(headers)

    print("输出目录：" + out)
    print("  " + name + "  " + str(os.path.getsize(exe)) + " 字节")
    print("  version.json  version=" + version)
    print("                url=" + url)
    print("                sha256=" + meta["sha256"][:16] + "...")
    if args.base:
        print("把这个目录拖到 Cloudflare Pages 即可发布。")
    else:
        print("提示：加 --base https://xxx.pages.dev 才能生成完整下载地址。")
    return 0


if __name__ == "__main__":
    sys.exit(main())
