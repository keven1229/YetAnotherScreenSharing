FROM ossrs/srs:v6

# 复制配置文件
COPY srs.conf /usr/local/srs/conf/srs.conf

# 暴露端口
# RTMP
EXPOSE 1935
# HTTP Server (FLV/HLS)
EXPOSE 8080
# HTTP API
EXPOSE 1985

# 启动 SRS
CMD ["./objs/srs", "-c", "conf/srs.conf"]
