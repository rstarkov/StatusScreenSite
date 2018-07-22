const path = require('path');
const ManifestPlugin = require('webpack-manifest-plugin');
const DynamicCdnWebpackPlugin = require('dynamic-cdn-webpack-plugin');
const ExtractTextPlugin = require("extract-text-webpack-plugin");
const LessPluginAutoPrefix = require('less-plugin-autoprefix');
const UglifyPlugin = require("uglifyjs-webpack-plugin");

const extractLess = new ExtractTextPlugin({
  filename: "app.css"
});

module.exports = {

  entry: './Src/App.ts',

  output: {
    filename: 'app.js',
    sourceMapFilename: '[file].map',
    path: path.resolve(__dirname, '../Builds/Static')
  },

  devtool: 'source-map',

  resolve: {
    extensions: ['.ts', '.tsx', '.js', '.jsx', '.less']
  },

  module: {
    loaders: [
      {
        test: /\.tsx?$/,
        loader: 'awesome-typescript-loader'
      },
      {
        test: /\.less$/,
        use: extractLess.extract({
          use: [{
            loader: "css-loader"
          }, {
            loader: 'less-loader',
            options: {
              plugins: [
                new LessPluginAutoPrefix({ browsers: ["last 3 versions"] })
              ]
            }
          }]
        })
      }
    ]
  },

  plugins: [
    new UglifyPlugin(),
    new ManifestPlugin({
      fileName: 'manifest.json'
    }),
    new DynamicCdnWebpackPlugin({
      resolver: function (modulePath, version) {
        if (modulePath == 'lodash')
          return { name: 'lodash', url: 'https://cdnjs.cloudflare.com/ajax/libs/lodash.js/' + version + '/lodash.min.js', var: '_' };
        else if (modulePath == 'jquery')
          return { name: 'jquery', url: 'https://cdnjs.cloudflare.com/ajax/libs/jquery/' + version + '/jquery.min.js', var: '$' };
        else if (modulePath == 'plottable')
          return { name: 'plottable', url: 'https://cdnjs.cloudflare.com/ajax/libs/plottable.js/' + version + '/plottable.min.js', var: 'Plottable' };
        else if (modulePath == 'd3')
          return { name: 'd3', url: 'https://cdnjs.cloudflare.com/ajax/libs/d3/' + version + '/d3.min.js', var: 'd3' };
        else if (modulePath == 'moment')
          return { name: 'moment', url: 'https://cdnjs.cloudflare.com/ajax/libs/moment.js/' + version + '/moment.min.js', var: 'moment' };
        else
          return null;
      }
    }),
    extractLess
  ]
};
