namespace IndyInitRootCauseInvestigator.Engine

open System
open System.Text

/// SHA-256, SHA-1 and UUIDv5 in plain managed code: identical results on the server and in the browser
/// (WebAssembly), with no dependency on a platform crypto provider. Integrity only, not authentication (contract §10.1).
[<RequireQualifiedAccess>]
module Hashing =
    let private rotr (x: uint32) (n: int) = (x >>> n) ||| (x <<< (32 - n))
    let private rotl (x: uint32) (n: int) = (x <<< n) ||| (x >>> (32 - n))

    /// Message padding shared by SHA-1 and SHA-256 (big-endian length).
    let private pad (data: byte[]) =
        let bitLength = uint64 data.Length * 8UL
        let total = ((data.Length + 9 + 63) / 64) * 64
        let padded = Array.zeroCreate<byte> total
        Array.blit data 0 padded 0 data.Length
        padded[data.Length] <- 0x80uy
        for i in 0..7 do
            padded[total - 1 - i] <- byte (bitLength >>> (8 * i))
        padded

    let private word (b: byte[]) (i: int) =
        (uint32 b[i] <<< 24) ||| (uint32 b[i + 1] <<< 16) ||| (uint32 b[i + 2] <<< 8) ||| uint32 b[i + 3]

    let private k256 =
        [| 0x428a2f98u; 0x71374491u; 0xb5c0fbcfu; 0xe9b5dba5u; 0x3956c25bu; 0x59f111f1u; 0x923f82a4u; 0xab1c5ed5u
           0xd807aa98u; 0x12835b01u; 0x243185beu; 0x550c7dc3u; 0x72be5d74u; 0x80deb1feu; 0x9bdc06a7u; 0xc19bf174u
           0xe49b69c1u; 0xefbe4786u; 0x0fc19dc6u; 0x240ca1ccu; 0x2de92c6fu; 0x4a7484aau; 0x5cb0a9dcu; 0x76f988dau
           0x983e5152u; 0xa831c66du; 0xb00327c8u; 0xbf597fc7u; 0xc6e00bf3u; 0xd5a79147u; 0x06ca6351u; 0x14292967u
           0x27b70a85u; 0x2e1b2138u; 0x4d2c6dfcu; 0x53380d13u; 0x650a7354u; 0x766a0abbu; 0x81c2c92eu; 0x92722c85u
           0xa2bfe8a1u; 0xa81a664bu; 0xc24b8b70u; 0xc76c51a3u; 0xd192e819u; 0xd6990624u; 0xf40e3585u; 0x106aa070u
           0x19a4c116u; 0x1e376c08u; 0x2748774cu; 0x34b0bcb5u; 0x391c0cb3u; 0x4ed8aa4au; 0x5b9cca4fu; 0x682e6ff3u
           0x748f82eeu; 0x78a5636fu; 0x84c87814u; 0x8cc70208u; 0x90befffau; 0xa4506cebu; 0xbef9a3f7u; 0xc67178f2u |]

    let private toBytes (words: uint32[]) =
        words |> Array.collect (fun w -> [| byte (w >>> 24); byte (w >>> 16); byte (w >>> 8); byte w |])

    let sha256 (data: byte[]) : byte[] =
        let m = pad data
        let h = [| 0x6a09e667u; 0xbb67ae85u; 0x3c6ef372u; 0xa54ff53au; 0x510e527fu; 0x9b05688cu; 0x1f83d9abu; 0x5be0cd19u |]
        let w = Array.zeroCreate<uint32> 64
        for chunk in 0 .. (m.Length / 64) - 1 do
            let o = chunk * 64
            for t in 0..15 do
                w[t] <- word m (o + 4 * t)
            for t in 16..63 do
                let s0 = rotr w[t - 15] 7 ^^^ rotr w[t - 15] 18 ^^^ (w[t - 15] >>> 3)
                let s1 = rotr w[t - 2] 17 ^^^ rotr w[t - 2] 19 ^^^ (w[t - 2] >>> 10)
                w[t] <- w[t - 16] + s0 + w[t - 7] + s1
            let mutable a = h[0]
            let mutable b = h[1]
            let mutable c = h[2]
            let mutable d = h[3]
            let mutable e = h[4]
            let mutable f = h[5]
            let mutable g = h[6]
            let mutable hh = h[7]
            for t in 0..63 do
                let S1 = rotr e 6 ^^^ rotr e 11 ^^^ rotr e 25
                let ch = (e &&& f) ^^^ ((~~~e) &&& g)
                let t1 = hh + S1 + ch + k256[t] + w[t]
                let S0 = rotr a 2 ^^^ rotr a 13 ^^^ rotr a 22
                let maj = (a &&& b) ^^^ (a &&& c) ^^^ (b &&& c)
                let t2 = S0 + maj
                hh <- g
                g <- f
                f <- e
                e <- d + t1
                d <- c
                c <- b
                b <- a
                a <- t1 + t2
            h[0] <- h[0] + a
            h[1] <- h[1] + b
            h[2] <- h[2] + c
            h[3] <- h[3] + d
            h[4] <- h[4] + e
            h[5] <- h[5] + f
            h[6] <- h[6] + g
            h[7] <- h[7] + hh
        toBytes h

    let sha1 (data: byte[]) : byte[] =
        let m = pad data
        let h = [| 0x67452301u; 0xEFCDAB89u; 0x98BADCFEu; 0x10325476u; 0xC3D2E1F0u |]
        let w = Array.zeroCreate<uint32> 80
        for chunk in 0 .. (m.Length / 64) - 1 do
            let o = chunk * 64
            for t in 0..15 do
                w[t] <- word m (o + 4 * t)
            for t in 16..79 do
                w[t] <- rotl (w[t - 3] ^^^ w[t - 8] ^^^ w[t - 14] ^^^ w[t - 16]) 1
            let mutable a = h[0]
            let mutable b = h[1]
            let mutable c = h[2]
            let mutable d = h[3]
            let mutable e = h[4]
            for t in 0..79 do
                let f, k =
                    if t < 20 then (b &&& c) ||| ((~~~b) &&& d), 0x5A827999u
                    elif t < 40 then b ^^^ c ^^^ d, 0x6ED9EBA1u
                    elif t < 60 then (b &&& c) ||| (b &&& d) ||| (c &&& d), 0x8F1BBCDCu
                    else b ^^^ c ^^^ d, 0xCA62C1D6u
                let temp = rotl a 5 + f + e + k + w[t]
                e <- d
                d <- c
                c <- rotl b 30
                b <- a
                a <- temp
            h[0] <- h[0] + a
            h[1] <- h[1] + b
            h[2] <- h[2] + c
            h[3] <- h[3] + d
            h[4] <- h[4] + e
        toBytes h

    let hex (bytes: byte[]) =
        let sb = StringBuilder(bytes.Length * 2)
        for b in bytes do
            sb.Append(b.ToString("x2")) |> ignore
        sb.ToString()

    let sha256Hex (data: byte[]) = hex (sha256 data)
    let sha256OfText (s: string) = sha256Hex (Encoding.UTF8.GetBytes s)

    /// RFC 4122 big-endian bytes of a UUID.
    let private uuidBytes (g: Guid) =
        let s = g.ToString("N")
        Array.init 16 (fun i -> Convert.ToByte(s.Substring(2 * i, 2), 16))

    let private ofUuidBytes (b: byte[]) = Guid(hex b)

    /// UUIDv5 (name-based, SHA-1) per RFC 4122 §4.3.
    let uuidV5 (ns: Guid) (name: string) : Guid =
        let hash = sha1 (Array.append (uuidBytes ns) (Encoding.UTF8.GetBytes name))
        let b = Array.sub hash 0 16
        b[6] <- (b[6] &&& 0x0Fuy) ||| 0x50uy
        b[8] <- (b[8] &&& 0x3Fuy) ||| 0x80uy
        ofUuidBytes b

    let namespaceUrl = Guid "6ba7b811-9dad-11d1-80b4-00c04fd430c8"

    /// N = UUIDv5(NAMESPACE_URL, ".../contract/v1/event-id") = 241fefd4-c810-5a3a-9cfa-4bbd968ae1cb (contract §5.2).
    let eventIdNamespace =
        uuidV5 namespaceUrl "https://github.com/kemiller2002/causa-data/contract/v1/event-id"

    /// eventId = UUIDv5(N, investigationId + "/" + aggregateVersion + "/" + correlationId) (contract §5.2).
    let eventId (investigation: Guid) (version: int64) (correlation: Guid) =
        uuidV5 eventIdNamespace (investigation.ToString("D") + "/" + string version + "/" + correlation.ToString("D"))
